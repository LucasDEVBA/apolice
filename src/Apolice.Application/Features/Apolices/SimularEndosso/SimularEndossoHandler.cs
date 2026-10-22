using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Comum;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Features.Apolices.SimularEndosso;

public sealed class SimularEndossoHandler(PreparacaoEndosso preparacao, ITarifador tarifador, TimeProvider tempo)
    : ICommandHandler<SimularEndossoCommand, SimulacaoEndossoDto>
{
    public async Task<Result<SimulacaoEndossoDto>> HandleAsync(SimularEndossoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var preparado = await preparacao.PrepararAsync(command.ApoliceId, command.Tipo, command.CoberturaCodigo, command.Lmi, cancellationToken);
        if (preparado.IsFailure)
        {
            return preparado.Error;
        }

        var (carregada, produto, solicitacao) = preparado.Value;
        var simulacao = carregada.Apolice.SimularEndosso(solicitacao, produto, tarifador, Relogio.Hoje(tempo));
        return simulacao.IsSuccess ? SimulacaoEndossoDto.De(simulacao.Value) : simulacao.Error;
    }
}
