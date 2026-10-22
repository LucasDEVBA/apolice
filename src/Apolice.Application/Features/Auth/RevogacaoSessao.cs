using Apolice.Application.Abstractions.Data;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Features.Auth;

// Encerrar a sessão = revogar a família inteira (todas as rotações do mesmo login), não só o token apresentado.
public sealed class RevogacaoSessao(IRefreshTokenRepository refreshTokens)
{
    public async Task RevogarFamiliaAsync(RefreshToken token, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.Revogar(agora);
        foreach (var membro in await refreshTokens.ListarNaoRevogadosDaFamiliaAsync(token.FamiliaId, cancellationToken))
        {
            membro.Revogar(agora);
        }
    }
}
