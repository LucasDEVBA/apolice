using Apolice.Domain.Usuarios;

namespace Apolice.Application.Abstractions.Data;

public interface IRefreshTokenRepository
{
    // Busca pelo hash do token (RefreshToken.CalcularHash): o token em texto nunca é persistido.
    Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken);

    // Só os tokens da família ainda não revogados (revogado_em IS NULL), com tracking. Revogar é idempotente e uma
    // família de 7 dias com rotação a cada 15 min acumula centenas de linhas já revogadas que não precisam voltar.
    Task<IReadOnlyList<RefreshToken>> ListarNaoRevogadosDaFamiliaAsync(Guid familiaId, CancellationToken cancellationToken);

    void Adicionar(RefreshToken token);
}
