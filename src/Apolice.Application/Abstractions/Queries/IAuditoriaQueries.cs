using Apolice.Application.Common;
using Apolice.Application.Features.Auditoria;

namespace Apolice.Application.Abstractions.Queries;

// Keyset por (ocorrido_em, id) DESC — use Pagina.Montar com limite + 1 linhas.
public interface IAuditoriaQueries
{
    Task<Pagina<AuditoriaDto>> ListarAsync(PosicaoCursor? apos, int limite, CancellationToken cancellationToken);
}
