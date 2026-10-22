using Amazon;
using Amazon.DSQL.Util;
using Apolice.Infrastructure.Configuracao;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Provedores;

// Um único NpgsqlDataSource (pool) por processo, usado pelo EF (UseNpgsql(dataSource)), pelo Dapper e pelo numerador.
// No DSQL a autenticação é por token IAM (sem senha), gerado a CADA abertura de conexão física (UsePasswordProvider).
// Um provedor periódico (timer) não serve na Lambda: o processo congela entre invocações, o timer não roda, e depois
// de um congelamento maior que a validade do token a primeira conexão nova abriria com token vencido (erro de
// autenticação, que não é transitório). A assinatura SigV4 é local e barata, e só acontece quando o pool abre uma
// conexão física. O registro em DI continua síncrono (sem o DsqlDataSource.CreateAsync, que exigiria sync-over-async),
// e as credenciais são resolvidas a cada token pela cadeia padrão do SDK, o que cobre a rotação na Lambda.
internal static class FonteDadosNpgsql
{
    private const string BancoDsql = "postgres";
    private const int PortaDsql = 5432;
    private const string NomeAplicacao = "apolice";

    // O token só é conferido no handshake da conexão: a validade cobre apenas o intervalo entre gerar e conectar.
    private static readonly TimeSpan ValidadeToken = TimeSpan.FromMinutes(15);

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
        builder.UsePasswordProvider(
            _ => admin
                ? DSQLAuthTokenGenerator.GenerateDbConnectAdminAuthToken(regiao, host, ValidadeToken)
                : DSQLAuthTokenGenerator.GenerateDbConnectAuthToken(regiao, host, ValidadeToken),
            async (_, _) => admin
                ? await DSQLAuthTokenGenerator.GenerateDbConnectAdminAuthTokenAsync(regiao, host, ValidadeToken)
                : await DSQLAuthTokenGenerator.GenerateDbConnectAuthTokenAsync(regiao, host, ValidadeToken));
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
