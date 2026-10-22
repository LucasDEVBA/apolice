using Apolice.Domain.Produtos;

namespace Apolice.Application.Abstractions.Data;

// Fonte da verdade do catálogo (com tracking e coberturas): alteração de taxa e todo caso de uso que grava prêmio
// (criar cotação, endosso). GET e simulação de cotação usam ICatalogoProdutos, que pode estar defasado em outra instância.
public interface IProdutoRepository
{
    Task<Produto?> ObterAsync(string codigo, CancellationToken cancellationToken);

    Task<IReadOnlyList<Produto>> ListarAsync(CancellationToken cancellationToken);
}
