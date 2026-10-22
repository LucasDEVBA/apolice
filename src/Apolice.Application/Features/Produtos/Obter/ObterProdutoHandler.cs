using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Produtos.Obter;

public sealed class ObterProdutoHandler(ICatalogoProdutos catalogo, ICurrentUser usuario) : IQueryHandler<ObterProdutoQuery, ProdutoDto>
{
    public async Task<Result<ProdutoDto>> HandleAsync(ObterProdutoQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var autenticado = usuario.ExigirAutenticado();
        if (autenticado.IsFailure)
        {
            return autenticado.Error;
        }

        var codigo = CodigoCatalogo.Normalizar(query.Codigo);
        var produto = codigo is null ? null : await catalogo.ObterAsync(codigo, cancellationToken);
        return produto is null ? ComumErros.NaoEncontrado(Entidades.Produto) : ProdutoMapeamento.ParaDto(produto);
    }
}
