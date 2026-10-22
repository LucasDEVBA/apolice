using Apolice.Domain.Produtos;

namespace Apolice.Application.Abstractions.Services;

// Leitura cacheada do catálogo (HybridCache na infra) para GET /produtos, simulação de cotação e textos de notificação.
// Cada chamada devolve instâncias exclusivas do chamador (ex.: reconstituídas com Produto.Criar a partir de um snapshot
// imutável guardado no cache): o agregado é mutável, e uma instância compartilhada alterada por engano corromperia
// todas as requisições. Quem grava prêmio ou altera o produto usa IProdutoRepository, porque o L1 de outra instância
// pode ficar defasado até expirar.
public interface ICatalogoProdutos
{
    Task<IReadOnlyList<Produto>> ListarAsync(CancellationToken cancellationToken);

    Task<Produto?> ObterAsync(string codigo, CancellationToken cancellationToken);

    Task InvalidarAsync(CancellationToken cancellationToken);
}
