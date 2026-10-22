using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Abstractions.Data;

public interface IUsuarioRepository
{
    // E-mail já normalizado pelo VO (trim + minúsculas): a busca deve ser por igualdade exata.
    Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken cancellationToken);

    Task<Usuario?> ObterAsync(Guid id, CancellationToken cancellationToken);
}
