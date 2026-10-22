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
    private static readonly TimeSpan EsperaOcc = TimeSpan.FromMilliseconds(250);
    private static readonly string[] JaExiste = ["42P07", "42710", "42701", "42P06", "42P16"];

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

    // CREATE INDEX ASYNC devolve o id do job de construção; esperar por ele garante que índices únicos já valem
    // quando a aplicação subir. A espera é best-effort: se o sys.wait_for_job falhar, o índice segue sendo construído.
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

        try
        {
            await ComRetentativaAsync(async conexao =>
            {
                await using var espera = new NpgsqlCommand("SELECT sys.wait_for_job(@job)", conexao);
                espera.Parameters.AddWithValue("job", job.ToString()!);
                return await espera.ExecuteScalarAsync(cancellationToken);
            }, "sys.wait_for_job", cancellationToken);
        }
        catch (PostgresException ex)
        {
            LogEsperaIndiceFalhou(logger, ex, job.ToString());
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

        foreach (var arn in configuracao.AppRoleArns)
        {
            await ExecutarAsync($"AWS IAM GRANT {identificador} TO '{arn}'", cancellationToken);
        }

        if (configuracao.AppRoleArns.Count == 0)
        {
            LogSemArns(logger, papel);
        }

        await ExecutarAsync($"GRANT USAGE ON SCHEMA public TO {identificador}", cancellationToken);
        foreach (var tabela in Tabelas())
        {
            await ExecutarAsync($"GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE \"{tabela}\" TO {identificador}", cancellationToken);
        }

        LogPapel(logger, papel, configuracao.AppRoleArns.Count);
    }

    private IEnumerable<string> Tabelas() =>
        db.Model.GetEntityTypes().Select(t => t.GetTableName()).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    // Reexecução: objeto que já existe (tabela, índice, vínculo IAM) é o estado desejado, não erro.
    private async Task ExecutarAsync(string sql, CancellationToken cancellationToken)
    {
        try
        {
            await ComRetentativaAsync(async conexao =>
            {
                await using var comando = new NpgsqlCommand(sql, conexao);
                return await comando.ExecuteNonQueryAsync(cancellationToken);
            }, sql, cancellationToken);
        }
        catch (PostgresException ex) when (JaExiste.Contains(ex.SqlState) || ex.MessageText.Contains("already", StringComparison.OrdinalIgnoreCase))
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

    [LoggerMessage(Level = LogLevel.Information, Message = "DSQL: papel {Papel} com GRANTs aplicados e {Arns} role(s) IAM vinculada(s).")]
    private static partial void LogPapel(ILogger logger, string papel, int arns);
}
