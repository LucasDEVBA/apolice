using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Produtos.Listar;

public sealed class ListarProdutosHandler(ICatalogoProdutos catalogo, ICurrentUser usuario)
    : IQueryHandler<ListarProdutosQuery, IReadOnlyList<ProdutoDto>>
{
    public async Task<Result<IReadOnlyList<ProdutoDto>>> HandleAsync(ListarProdutosQuery query, CancellationToken cancellationToken)
    {
        // O catálogo não é sensível, mas o handler não confia que a política do endpoint foi aplicada (como os demais).
        var autenticado = usuario.ExigirAutenticado();
        if (autenticado.IsFailure)
        {
            return autenticado.Error;
        }

        var produtos = await catalogo.ListarAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ProdutoDto>>([.. produtos.Select(ProdutoMapeamento.ParaDto)]);
    }
}
