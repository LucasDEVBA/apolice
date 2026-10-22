using System.Security.Claims;
using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Apolice.Api.Seguranca;

// Lê os claims do JWT já validado (MapInboundClaims = false: "sub", "name" e "role" chegam com o nome curto).
// Sem identidade autenticada tudo é nulo: um principal anônimo nunca vira "usuário sem filtro" na aplicação.
internal sealed class HttpCurrentUser(IHttpContextAccessor acessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal =>
        acessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal ? principal : null;

    public Guid? UsuarioId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) && id != Guid.Empty ? id : null;

    public string? Nome => Principal?.FindFirstValue(JwtRegisteredClaimNames.Name);

    // Só nomes exatos do enum: "1" ou "admin" num token forjado não podem virar papel (TryParse aceitaria números).
    public Papel? Papel =>
        Principal?.FindFirstValue(JwtOptions.ClaimPapel) is { } papel && Enum.GetNames<Papel>().Contains(papel, StringComparer.Ordinal)
            ? Enum.Parse<Papel>(papel)
            : null;

    public bool Autenticado => UsuarioId is not null;
}
