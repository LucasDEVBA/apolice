using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Auth.Me;

public sealed class ObterUsuarioAtualHandler(IUsuarioRepository usuarios, ICurrentUser usuarioAtual)
    : IQueryHandler<ObterUsuarioAtualQuery, UsuarioDto>
{
    public async Task<Result<UsuarioDto>> HandleAsync(ObterUsuarioAtualQuery query, CancellationToken cancellationToken)
    {
        var id = usuarioAtual.ExigirAutenticado();
        if (id.IsFailure)
        {
            return id.Error;
        }

        // Access token ainda válido de usuário desativado depois da emissão: tratado como não autenticado.
        var usuario = await usuarios.ObterAsync(id.Value, cancellationToken);
        return usuario is { Ativo: true } ? UsuarioDto.De(usuario) : ComumErros.NaoAutenticado;
    }
}
