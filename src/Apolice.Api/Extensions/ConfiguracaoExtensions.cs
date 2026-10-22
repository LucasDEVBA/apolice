using System.Reflection;
using System.Security.Cryptography;

namespace Apolice.Api.Extensions;

internal static class ConfiguracaoExtensions
{
    public const string ChavePrefixoSsm = "Ssm:Prefixo";
    private const string NomeGeradorDocumento = "GetDocument.Insider";
    private const int TamanhoChaveEfemeraBytes = 64;

    // O build gera docs/openapi/v1.json executando este Program com um servidor nulo (GetDocument.Insider).
    public static bool GerandoDocumentoOpenApi =>
        string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, NomeGeradorDocumento, StringComparison.Ordinal);

    public static WebApplicationBuilder AddConfiguracaoApolice(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // No-op fora da Lambda: o mesmo binário roda no Kestrel (local/container) e atrás da Function URL.
        builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

        if (GerandoDocumentoOpenApi)
        {
            builder.Configuration.AddInMemoryCollection(ConfiguracaoGeracaoDocumento());
            return builder;
        }

        if (builder.Configuration[ChavePrefixoSsm] is { Length: > 0 } prefixo)
        {
            // Obrigatório: em produção os segredos (Jwt:SigningKey) vêm daqui; sem eles a API não pode subir "meio configurada".
            builder.Configuration.AddSystemsManager(fonte =>
            {
                fonte.Path = prefixo;
                fonte.Optional = false;
            });
        }

        return builder;
    }

    // A geração do documento sobe o host (ValidateOnStart roda), mas nunca abre conexão: bastam valores coerentes.
    // A chave é aleatória por execução para não existir nenhum segredo fixo no código.
    private static Dictionary<string, string?> ConfiguracaoGeracaoDocumento() => new()
    {
        ["Database:Provider"] = "Postgres",
        ["ConnectionStrings:Apolice"] = "Host=localhost;Database=apolice_openapi",
        ["Messaging:Transport"] = "InMemory",
        ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoChaveEfemeraBytes)),
    };
}
