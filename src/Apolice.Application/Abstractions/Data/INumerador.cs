namespace Apolice.Application.Abstractions.Data;

// Sequencial atômico por chave (INSERT ... ON CONFLICT ... RETURNING): roda fora do SaveChanges,
// então uma emissão que falha depois de numerar deixa lacuna — aceito pelo SPEC.
public interface INumerador
{
    Task<long> ProximoAsync(string chave, CancellationToken cancellationToken);
}
