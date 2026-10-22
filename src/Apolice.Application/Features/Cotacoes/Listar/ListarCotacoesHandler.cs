using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Cotacoes.Listar;

public sealed class ListarCotacoesHandler(ICotacaoQueries consultas, ICurrentUser usuario, TimeProvider tempo)
    : IQueryHandler<ListarCotacoesQuery, Pagina<CotacaoResumoDto>>
{
    public async Task<Result<Pagina<CotacaoResumoDto>>> HandleAsync(ListarCotacoesQuery query, CancellationToken cancellationToken)
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

        var filtro = new FiltroCotacoes(
            escopo.Value.CorretorId,
            query.Status,
            CodigoCatalogo.Normalizar(query.Produto),
            Paginacao.NormalizarFiltro(query.Busca),
            pagina.Value.Apos,
            pagina.Value.Limite,
            tempo.GetUtcNow());
        return await consultas.ListarAsync(filtro, cancellationToken);
    }
}
