using Apolice.Domain.Usuarios;

namespace Apolice.Application.Abstractions.Security;

public interface ITokenService
{
    // Validade do refresh token vinda da configuração (Jwt:RefreshTokenDias): a aplicação cria o
    // RefreshToken do domínio e precisa dela, mas não deve ler configuração.
    TimeSpan ValidadeRefreshToken { get; }

    (string Token, DateTimeOffset ExpiraEm) GerarAccessToken(Usuario usuario);

    // Texto aleatório entregue só no cookie; o banco guarda apenas RefreshToken.CalcularHash(token) — o hash é regra
    // do domínio (SPEC 4.6), não desta porta, para que uma implementação diferente não invalide todas as sessões.
    string GerarRefreshToken();
}
