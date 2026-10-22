using Apolice.Domain.Cotacoes;

namespace Apolice.Application.Abstractions.Data;

public interface ICotacaoRepository
{
    // Com tracking e com itens/fatores: o agregado volta pronto para mutação.
    Task<Cotacao?> ObterAsync(Guid id, CancellationToken cancellationToken);

    void Adicionar(Cotacao cotacao);
}
