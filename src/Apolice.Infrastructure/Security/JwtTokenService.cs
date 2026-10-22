using System.Buffers.Text;
using System.Security.Cryptography;
using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Apolice.Infrastructure.Security;

// Access token HS256 curto (Jwt:AccessTokenMinutos) com os claims do SPEC 6; refresh token = 32 bytes aleatórios
// em base64url (só o hash SHA-256 vai para o banco, regra do domínio).
internal sealed class JwtTokenService(IOptions<JwtOptions> opcoes, TimeProvider tempo) : ITokenService
{
    private const int TamanhoRefreshTokenBytes = 32;
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public TimeSpan ValidadeRefreshToken => TimeSpan.FromDays(opcoes.Value.RefreshTokenDias);

    public (string Token, DateTimeOffset ExpiraEm) GerarAccessToken(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var configuracao = opcoes.Value;
        var agora = tempo.GetUtcNow();
        var expiraEm = agora.AddMinutes(configuracao.AccessTokenMinutos);
        var descritor = new SecurityTokenDescriptor
        {
            Issuer = configuracao.Issuer,
            Audience = configuracao.Audience,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(configuracao.ChaveAssinatura(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                [JwtRegisteredClaimNames.Name] = usuario.Nome,
                [JwtRegisteredClaimNames.Email] = usuario.Email.Valor,
                [JwtOptions.ClaimPapel] = usuario.Papel.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7(agora).ToString(),
            },
        };

        return (_handler.CreateToken(descritor), expiraEm);
    }

    public string GerarRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TamanhoRefreshTokenBytes));
}
