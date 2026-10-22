using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Apolices.Obter;

public sealed class ObterApoliceHandler(IApoliceQueries consultas, ICurrentUser usuario, TimeProvider tempo)
    : IQueryHandler<ObterApoliceQuery, ApoliceDto>
{
    public async Task<Result<ApoliceDto>> HandleAsync(ObterApoliceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var escopo = usuario.Escopo();
        if (escopo.IsFailure)
        {
            return escopo.Error;
        }

        // A carteira é conferida também sobre o que voltou: um filtro esquecido no SQL não vira BOLA.
        var apolice = await consultas.ObterAsync(query.ApoliceId, escopo.Value.CorretorId, Relogio.Hoje(tempo), cancellationToken);
        return apolice is not null && escopo.Value.Alcanca(apolice.CorretorId) ? apolice : ComumErros.NaoEncontrado(Entidades.Apolice);
    }
}
