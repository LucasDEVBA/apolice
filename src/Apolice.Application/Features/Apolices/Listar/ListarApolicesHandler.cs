using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Apolices.Listar;

public sealed class ListarApolicesHandler(IApoliceQueries consultas, ICurrentUser usuario, TimeProvider tempo)
    : IQueryHandler<ListarApolicesQuery, Pagina<ApoliceResumoDto>>
{
    public async Task<Result<Pagina<ApoliceResumoDto>>> HandleAsync(ListarApolicesQuery query, CancellationToken cancellationToken)
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

        var filtro = new FiltroApolices(
            escopo.Value.CorretorId,
            query.Status,
            CodigoCatalogo.Normalizar(query.Produto),
            Paginacao.NormalizarFiltro(query.Busca),
            pagina.Value.Apos,
            pagina.Value.Limite,
            Relogio.Hoje(tempo));
        return await consultas.ListarAsync(filtro, cancellationToken);
    }
}
