using Amazon;
using Amazon.DSQL.Util;
using Apolice.Infrastructure.Configuracao;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Provedores;

// Um único NpgsqlDataSource (pool) por processo, usado pelo EF (UseNpgsql(dataSource)), pelo Dapper e pelo numerador.
// No DSQL a autenticação é por token IAM (sem senha): o token vem do UsePeriodicPasswordProvider, que o gera de forma
// assíncrona em segundo plano e o reaproveita em todas as conexões físicas até a próxima renovação. Assim o registro
// em DI é síncrono (sem sync-over-async, que o DsqlDataSource.CreateAsync exigiria) e nenhuma abertura de conexão
// paga a assinatura SigV4. As credenciais são resolvidas a cada renovação pela cadeia padrão do SDK, o que cobre
// rotação de credenciais na Lambda.
internal static class FonteDadosNpgsql
{
    private const string BancoDsql = "postgres";
    private const int PortaDsql = 5432;
    private const string NomeAplicacao = "apolice";

    // O token IAM vale 15 min; renovar aos 10 deixa margem para conexões abertas perto da troca.
    private static readonly TimeSpan ValidadeToken = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RenovacaoToken = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetentativaToken = TimeSpan.FromSeconds(10);

    // O DSQL encerra conexões com 1 h; reciclar antes evita o erro no meio de uma transação.
    private static readonly TimeSpan VidaMaximaConexao = TimeSpan.FromMinutes(55);
    private const int TamanhoMaximoPool = 20;

    public static NpgsqlDataSource Criar(DatabaseOptions opcoes, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        var builder = opcoes.Provider == ProvedorBanco.Dsql
            ? CriarBuilderDsql(opcoes)
            : new NpgsqlDataSourceBuilder(opcoes.ConnectionString);
        builder.UseLoggerFactory(loggerFactory);
        return builder.Build();
    }

    private static NpgsqlDataSourceBuilder CriarBuilderDsql(DatabaseOptions opcoes)
    {
        var host = opcoes.DsqlEndpoint!;
        var conexao = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = PortaDsql,
            Database = BancoDsql,
            Username = opcoes.DsqlUser,
            SslMode = SslMode.VerifyFull,
            SslNegotiation = SslNegotiation.Direct,
            ApplicationName = NomeAplicacao,
            MaxPoolSize = TamanhoMaximoPool,
            ConnectionLifetime = (int)VidaMaximaConexao.TotalSeconds,
            Enlist = false,

            // O DSQL não aceita DISCARD ALL, que o Npgsql envia ao devolver a conexão ao pool.
            NoResetOnClose = true,
        };

        var regiao = RegionEndpoint.GetBySystemName(RegiaoDoEndpoint(host));
        var admin = opcoes.UsuarioAdmin;
        var builder = new NpgsqlDataSourceBuilder(conexao.ConnectionString);
        builder.UsePeriodicPasswordProvider(
            async (_, _) => admin
                ? await DSQLAuthTokenGenerator.GenerateDbConnectAdminAuthTokenAsync(regiao, host, ValidadeToken)
                : await DSQLAuthTokenGenerator.GenerateDbConnectAuthTokenAsync(regiao, host, ValidadeToken),
            RenovacaoToken,
            RetentativaToken);
        return builder;
    }

    // <id>.dsql.<região>.on.aws (o formato é validado nas options).
    internal static string RegiaoDoEndpoint(string endpoint)
    {
        var partes = endpoint.Split('.');
        const int PosicaoRegiao = 2;
        return partes[PosicaoRegiao];
    }
}
