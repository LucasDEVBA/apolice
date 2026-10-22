using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Comum;

namespace Apolice.Application.Features.Apolices.SimularCancelamento;

public sealed class SimularCancelamentoHandler(AcessoApolice acesso, TimeProvider tempo)
    : ICommandHandler<SimularCancelamentoCommand, SimulacaoCancelamentoDto>
{
    public async Task<Result<SimulacaoCancelamentoDto>> HandleAsync(SimularCancelamentoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var carregada = await acesso.CarregarParaOperacaoAsync(command.ApoliceId, cancellationToken);
        if (carregada.IsFailure)
        {
            return carregada.Error;
        }

        var simulacao = carregada.Value.Apolice.SimularCancelamento(command.Motivo, Relogio.Hoje(tempo));
        return simulacao.IsSuccess ? SimulacaoCancelamentoDto.De(simulacao.Value) : simulacao.Error;
    }
}
