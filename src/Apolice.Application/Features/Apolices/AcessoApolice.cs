using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Apolices;

public sealed record ApoliceOperavel(ApoliceSeguro Apolice, Guid UsuarioId);

// Carga + autorização de carteira de uma apólice, comum a endosso, cancelamento e abertura de sinistro.
public sealed class AcessoApolice(IApoliceRepository apolices, ICurrentUser usuario)
{
    public async Task<Result<ApoliceOperavel>> CarregarParaOperacaoAsync(Guid apoliceId, CancellationToken cancellationToken)
    {
        // Autenticação antes de carregar: senão o anônimo receberia 404 para id inexistente e 401 para id existente.
        var autenticado = usuario.ExigirAutenticado();
        if (autenticado.IsFailure)
        {
            return autenticado.Error;
        }

        var apolice = await apolices.ObterAsync(apoliceId, cancellationToken);
        if (apolice is null)
        {
            return ComumErros.NaoEncontrado(Entidades.Apolice);
        }

        var acesso = usuario.AutorizarCarteira(apolice.CorretorId, Entidades.Apolice);
        return acesso.IsSuccess ? new ApoliceOperavel(apolice, acesso.Value) : acesso.Error;
    }
}
