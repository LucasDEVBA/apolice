using Apolice.Domain.Sinistros;

namespace Apolice.Application.Abstractions.Data;

public interface ISinistroRepository
{
    // Com tracking e histórico.
    Task<Sinistro?> ObterAsync(Guid id, CancellationToken cancellationToken);

    void Adicionar(Sinistro sinistro);
}
