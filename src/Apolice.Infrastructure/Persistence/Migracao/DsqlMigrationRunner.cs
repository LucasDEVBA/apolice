using Apolice.Infrastructure.Configuracao;
using Apolice.Infrastructure.Persistence.Provedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Migracao;

// Aplica as migrations do EF no Aurora DSQL sem o Database.Migrate (que usa LOCK TABLE e várias DDLs por transação,
// coisas que o DSQL não aceita): um comando por vez em autocommit, DDL adaptado pelo TransformadorSqlDsql,
// histórico gravado na tabela do próprio EF (o "dotnet ef" e o Postgres local continuam enxergando o mesmo estado),
// e por fim o papel da aplicação com privilégio mínimo e o vínculo com as roles IAM das Lambdas.
internal sealed partial class DsqlMigrationRunner(
    ApoliceDbContext db,
    NpgsqlDataSource fonte,
    IOptions<DatabaseOptions> opcoes,
    ILogger<DsqlMigrationRunner> logger)
{
    private const int TentativasOcc = 5;

    // A construção de um índice numa tabela grande passa fácil dos 30 s padrão do comando; o deploy tem timeout próprio.
    private const int TimeoutEsperaIndiceSegundos = 15 * 60;
    private const string TabelaAuditoria = "auditoria";
    private const string PrivilegiosCompletos = "SELECT, INSERT, UPDATE, DELETE";

    // A trilha de auditoria é só de inserção: a aplicação comprometida não consegue apagá-la nem reescrevê-la.
    private const string PrivilegiosSoInsercao = "SELECT, INSERT";

    private static readonly TimeSpan EsperaOcc = TimeSpan.FromMilliseconds(250);

    // Só "o objeto já existe" de verdade: tabela/índice (42P07), papel ou restrição (42710), coluna (42701), schema
    // (42P06). Um DDL inválido de uma migration futura tem de falhar o deploy, nunca ser registrado como aplicado.
    private static readonly string[] JaExiste = ["42P07", "42710", "42701", "42P06"];

    public async Task AplicarAsync(CancellationToken cancellationToken)
    {
        var historico = db.GetService<IHistoryRepository>();
        await ExecutarAsync(TransformadorSqlDsql.Transformar(historico.GetCreateIfNotExistsScript()), cancellationToken);
        var aplicadas = (await historico.GetAppliedMigrationsAsync(cancellationToken)).Select(h => h.MigrationId).ToHashSet(StringComparer.Ordinal);

        var assembly = db.GetService<IMigrationsAssembly>();
        foreach (var (id, tipo) in assembly.Migrations.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            if (aplicadas.Contains(id))
            {
                continue;
            }

            var migration = assembly.CreateMigration(tipo, db.Database.ProviderName!);
            await AplicarMigrationAsync(migration, cancellationToken);
            await ExecutarAsync(historico.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())), cancellationToken);
            LogMigrationAplicada(logger, id);
        }

        await ConcederPapelAsync(cancellationToken);
    }

    private async Task AplicarMigrationAsync(Migration migration, CancellationToken cancellationToken)
    {
        var modelo = migration.TargetModel is { } alvo ? db.GetService<IModelRuntimeInitializer>().Initialize(alvo) : null;
        var comandos = db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, modelo);
        foreach (var comando in comandos)
        {
            var sql = TransformadorSqlDsql.Transformar(comando.CommandText);
            if (TransformadorSqlDsql.CriaIndice(sql))
            {
                await CriarIndiceAsync(sql, cancellationToken);
            }
            else
            {
                await ExecutarAsync(sql, cancellationToken);
            }
        }
    }

    // CREATE INDEX ASYNC devolve o id do job de construção; esperar por ele garante que índices únicos já valem quando a
    // aplicação subir. Job que termina em falha (ex.: UNIQUE com duplicatas) derruba o deploy: registrar a migration
    // como aplicada deixaria o schema sem a garantia. Só a indisponibilidade da própria espera é tolerada.
    private async Task CriarIndiceAsync(string sql, CancellationToken cancellationToken)
    {
        var job = await ComRetentativaAsync(async conexao =>
        {
            await using var comando = new NpgsqlCommand(sql, conexao);
            return await comando.ExecuteScalarAsync(cancellationToken);
        }, sql, cancellationToken);

        if (job is null or DBNull)
        {
            return;
        }

        object? concluido;
        try
        {
            concluido = await ComRetentativaAsync(async conexao =>
            {
                await using var espera = new NpgsqlCommand("SELECT sys.wait_for_job(@job)", conexao) { CommandTimeout = TimeoutEsperaIndiceSegundos };
                espera.Parameters.AddWithValue("job", job.ToString()!);
                return await espera.ExecuteScalarAsync(cancellationToken);
            }, "sys.wait_for_job", cancellationToken);
        }
        catch (PostgresException ex)
        {
            LogEsperaIndiceFalhou(logger, ex, job.ToString());
            return;
        }

        if (concluido is false)
        {
            throw new InvalidOperationException($"DSQL: o job {job} de construção do índice terminou em falha (consulte sys.jobs): {Resumo(sql)}");
        }
    }

    private async Task ConcederPapelAsync(CancellationToken cancellationToken)
    {
        var configuracao = opcoes.Value;
        var papel = configuracao.AppRole;
        var existe = await ComRetentativaAsync(async conexao =>
        {
            await using var comando = new NpgsqlCommand("SELECT 1 FROM pg_roles WHERE rolname = @papel", conexao);
            comando.Parameters.AddWithValue("papel", papel);
            return await comando.ExecuteScalarAsync(cancellationToken);
        }, "pg_roles", cancellationToken);

        // Identificador e ARNs entram no texto do DDL (não há parâmetro em CREATE ROLE / AWS IAM GRANT): os formatos
        // foram validados nas options (ValidacaoDatabaseOptions) e são citados aqui.
        var identificador = $"\"{papel}\"";
        if (existe is null)
        {
            await ExecutarAsync($"CREATE ROLE {identificador} WITH LOGIN", cancellationToken);
        }

        await VincularRolesIamAsync(identificador, papel, configuracao.AppRoleArns, cancellationToken);

        await ExecutarAsync($"GRANT USAGE ON SCHEMA public TO {identificador}", cancellationToken);
        foreach (var tabela in Tabelas())
        {
            var privilegios = string.Equals(tabela, TabelaAuditoria, StringComparison.Ordinal) ? PrivilegiosSoInsercao : PrivilegiosCompletos;
            await ExecutarAsync($"GRANT {privilegios} ON TABLE \"{tabela}\" TO {identificador}", cancellationToken);
        }

        LogPapel(logger, papel, configuracao.AppRoleArns.Count);
    }

    // O vínculo vigente vem de sys.iam_pg_role_mappings, como o papel vem de pg_roles: só os ARNs que faltam recebem
    // AWS IAM GRANT (repetir o GRANT depende de como o DSQL responde, o que não foi verificado). Os que saíram da
    // configuração recebem AWS IAM REVOKE, mas só quando TODOS os configurados já constavam da visão: isso prova que o
    // formato do ARN gravado é o mesmo do configurado; sem essa prova, revogar poderia cortar o acesso da aplicação.
    // Configuração vazia nunca revoga. Se a visão não puder ser lida, volta ao GRANT de todos, tolerando o "já existe".
    private async Task VincularRolesIamAsync(string identificador, string papel, ICollection<string> arns, CancellationToken cancellationToken)
    {
        if (arns.Count == 0)
        {
            LogSemArns(logger, papel);
            return;
        }

        var vinculados = await ArnsVinculadosAsync(papel, cancellationToken);
        var faltantes = arns.Where(a => vinculados?.Contains(a) != true).ToList();
        foreach (var arn in faltantes)
        {
            await ExecutarAsync($"AWS IAM GRANT {identificador} TO '{arn}'", cancellationToken, toleraJaVinculado: true);
        }

        if (vinculados is null || faltantes.Count > 0)
        {
            return;
        }

        foreach (var arn in vinculados.Except(arns, StringComparer.Ordinal).Where(a => !a.Contains('\'', StringComparison.Ordinal)))
        {
            await ExecutarAsync($"AWS IAM REVOKE {identificador} FROM '{arn}'", cancellationToken);
            LogArnRevogado(logger, papel);
        }
    }

    private async Task<HashSet<string>?> ArnsVinculadosAsync(string papel, CancellationToken cancellationToken)
    {
        try
        {
            return await ComRetentativaAsync(async conexao =>
            {
                await using var comando = new NpgsqlCommand("SELECT arn FROM sys.iam_pg_role_mappings WHERE pg_role_name = @papel", conexao);
                comando.Parameters.AddWithValue("papel", papel);
                var arns = new HashSet<string>(StringComparer.Ordinal);
                await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
                while (await leitor.ReadAsync(cancellationToken))
                {
                    arns.Add(leitor.GetString(0));
                }

                return arns;
            }, "sys.iam_pg_role_mappings", cancellationToken);
        }
        catch (PostgresException ex)
        {
            LogMapeamentosIndisponiveis(logger, ex);
            return null;
        }
    }

    private IEnumerable<string> Tabelas() =>
        db.Model.GetEntityTypes().Select(t => t.GetTableName()).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    // Reexecução: objeto que já existe (tabela, índice, papel) é o estado desejado, não erro. O casamento por texto
    // ("already") vale só para o AWS IAM GRANT, cujo código de erro na repetição não é documentado.
    private async Task ExecutarAsync(string sql, CancellationToken cancellationToken, bool toleraJaVinculado = false)
    {
        try
        {
            await ComRetentativaAsync(async conexao =>
            {
                await using var comando = new NpgsqlCommand(sql, conexao);
                return await comando.ExecuteNonQueryAsync(cancellationToken);
            }, sql, cancellationToken);
        }
        catch (PostgresException ex) when (JaExiste.Contains(ex.SqlState)
            || (toleraJaVinculado && ex.MessageText.Contains("already", StringComparison.OrdinalIgnoreCase)))
        {
            LogJaExiste(logger, ex.SqlState, Resumo(sql));
        }
    }

    // Uma conexão por comando, sem transação explícita: no DSQL cada DDL precisa da própria transação (autocommit).
    private async Task<T> ComRetentativaAsync<T>(Func<NpgsqlConnection, Task<T>> acao, string descricao, CancellationToken cancellationToken)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await using var conexao = await fonte.OpenConnectionAsync(cancellationToken);
                return await acao(conexao);
            }
            catch (PostgresException ex) when (ErrosTransitorios.EhConflitoOcc(ex) && tentativa < TentativasOcc)
            {
                LogConflito(logger, tentativa, Resumo(descricao));
                await Task.Delay(EsperaOcc * tentativa, cancellationToken);
            }
        }
    }

    private static string Resumo(string sql) => sql.Length > 120 ? sql[..120] + "…" : sql;

    [LoggerMessage(Level = LogLevel.Information, Message = "DSQL: migration {MigrationId} aplicada.")]
    private static partial void LogMigrationAplicada(ILogger logger, string migrationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "DSQL: objeto já existente ({SqlState}), seguindo: {Sql}")]
    private static partial void LogJaExiste(ILogger logger, string sqlState, string sql);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DSQL: conflito de OCC na tentativa {Tentativa}, refazendo: {Sql}")]
    private static partial void LogConflito(ILogger logger, int tentativa, string sql);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DSQL: não foi possível aguardar o job de índice {Job}; o índice continua em construção.")]
    private static partial void LogEsperaIndiceFalhou(ILogger logger, Exception excecao, string? job);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DSQL: Database:AppRoleArns vazio; o papel {Papel} não foi vinculado a nenhuma role IAM.")]
    private static partial void LogSemArns(ILogger logger, string papel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DSQL: sys.iam_pg_role_mappings indisponível; AWS IAM GRANT para todos os ARNs configurados.")]
    private static partial void LogMapeamentosIndisponiveis(ILogger logger, Exception excecao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "DSQL: uma role IAM que saiu de Database:AppRoleArns foi desvinculada do papel {Papel}.")]
    private static partial void LogArnRevogado(ILogger logger, string papel);

    [LoggerMessage(Level = LogLevel.Information, Message = "DSQL: papel {Papel} com GRANTs aplicados e {Arns} role(s) IAM vinculada(s).")]
    private static partial void LogPapel(ILogger logger, string papel, int arns);
}
