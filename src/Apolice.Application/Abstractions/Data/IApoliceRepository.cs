using Apolice.Domain.Apolices;

namespace Apolice.Application.Abstractions.Data;

public interface IApoliceRepository
{
    // Com tracking, coberturas e endossos: endosso, cancelamento e sinistro dependem do agregado completo.
    Task<ApoliceSeguro?> ObterAsync(Guid id, CancellationToken cancellationToken);

    void Adicionar(ApoliceSeguro apolice);
}
