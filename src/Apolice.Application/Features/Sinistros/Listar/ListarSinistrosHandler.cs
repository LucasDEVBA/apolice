using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Sinistros.Listar;

public sealed class ListarSinistrosHandler(ISinistroQueries consultas, ICurrentUser usuario)
    : IQueryHandler<ListarSinistrosQuery, Pagina<SinistroResumoDto>>
{
    public async Task<Result<Pagina<SinistroResumoDto>>> HandleAsync(ListarSinistrosQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var escopo = usuario.Escopo();
        if (escopo.IsFailure)
        {
            return escopo.Error;
        }

        var pagina = Paginacao.Resolver(query.Cursor, query.Limite);
        if (pagina.IsFailure)
        {
            return pagina.Error;
        }

        var filtro = new FiltroSinistros(
            escopo.Value.CorretorId,
            query.Status,
            Paginacao.NormalizarFiltro(query.Busca),
            pagina.Value.Apos,
            pagina.Value.Limite);
        return await consultas.ListarAsync(filtro, cancellationToken);
    }
}
