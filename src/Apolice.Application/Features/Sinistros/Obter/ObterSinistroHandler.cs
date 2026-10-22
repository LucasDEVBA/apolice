using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Sinistros.Obter;

public sealed class ObterSinistroHandler(ISinistroQueries consultas, ICurrentUser usuario)
    : IQueryHandler<ObterSinistroQuery, SinistroDto>
{
    public async Task<Result<SinistroDto>> HandleAsync(ObterSinistroQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var escopo = usuario.Escopo();
        if (escopo.IsFailure)
        {
            return escopo.Error;
        }

        var sinistro = await consultas.ObterAsync(query.SinistroId, escopo.Value.CorretorId, cancellationToken);
        return sinistro is null ? ComumErros.NaoEncontrado(Entidades.Sinistro) : sinistro;
    }
}
