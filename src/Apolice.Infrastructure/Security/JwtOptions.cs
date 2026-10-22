using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Apolice.Infrastructure.Security;

// Seção "Jwt" do SPEC 13. A API usa as mesmas options (ChaveAssinatura) para validar o que este serviço emite.
public sealed class JwtOptions
{
    public const string Secao = "Jwt";
    public const int TamanhoMinimoChaveBytes = 32;

    // "role" curto (e não a URI de ClaimTypes.Role): a API valida com RoleClaimType = ClaimPapel.
    public const string ClaimPapel = "role";
    private const int AccessTokenMinutosPadrao = 15;
    private const int RefreshTokenDiasPadrao = 7;

    public string Issuer { get; set; } = "apolice-api";

    public string Audience { get; set; } = "apolice-web";

    public string? SigningKey { get; set; }

    public int AccessTokenMinutos { get; set; } = AccessTokenMinutosPadrao;

    public int RefreshTokenDias { get; set; } = RefreshTokenDiasPadrao;

    // A chave é texto (SSM SecureString); os bytes UTF-8 dele são o segredo do HMAC.
    public SymmetricSecurityKey ChaveAssinatura() =>
        new(Encoding.UTF8.GetBytes(SigningKey ?? throw new InvalidOperationException("Jwt:SigningKey não configurada.")));
}

internal sealed class ValidacaoJwtOptions : IValidateOptions<JwtOptions>
{
    private const int AccessTokenMinutosMaximo = 60;
    private const int RefreshTokenDiasMaximo = 30;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var falhas = new List<string>();
        if (options.SigningKey is null || Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.TamanhoMinimoChaveBytes)
        {
            falhas.Add($"Jwt:SigningKey deve ter ao menos {JwtOptions.TamanhoMinimoChaveBytes} bytes.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            falhas.Add("Jwt:Issuer e Jwt:Audience são obrigatórios.");
        }

        if (options.AccessTokenMinutos is < 1 or > AccessTokenMinutosMaximo)
        {
            falhas.Add($"Jwt:AccessTokenMinutos deve estar entre 1 e {AccessTokenMinutosMaximo}.");
        }

        if (options.RefreshTokenDias is < 1 or > RefreshTokenDiasMaximo)
        {
            falhas.Add($"Jwt:RefreshTokenDias deve estar entre 1 e {RefreshTokenDiasMaximo}.");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }
}

// Development sem chave: gera uma efêmera (uma por processo) em vez de exigir segredo para rodar local. Os tokens
// morrem a cada reinício, o que é aceitável fora de produção; fora de Development a validação barra a ausência.
internal sealed partial class ChaveEfemeraJwtDevelopment(IHostEnvironment ambiente, ILogger<ChaveEfemeraJwtDevelopment> logger)
    : IPostConfigureOptions<JwtOptions>
{
    private const int TamanhoChaveEfemeraBytes = 64;
    private readonly Lazy<string> _chave = new(() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoChaveEfemeraBytes)));

    public void PostConfigure(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!ambiente.IsDevelopment() || !string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return;
        }

        options.SigningKey = _chave.Value;
        LogChaveEfemera(logger);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jwt:SigningKey ausente em Development: usando chave efêmera; os tokens deixam de valer quando o processo reinicia.")]
    private static partial void LogChaveEfemera(ILogger logger);
}
