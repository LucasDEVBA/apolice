using Apolice.Domain.Comum;

namespace Apolice.Domain.Apolices;

public sealed partial class ApoliceSeguro
{
    // IOF não é restituído (simplificação documentada no SPEC): a base é só o prêmio líquido vigente.
    public Result<SimulacaoCancelamento> SimularCancelamento(MotivoCancelamento motivo, DateOnly hoje)
    {
        if (!Enum.IsDefined(motivo))
        {
            return ApoliceErros.MotivoCancelamentoInvalido;
        }

        var status = ExigirStatusAlteravel(hoje);
        return status.IsSuccess
            ? CalculadoraCancelamento.Simular(motivo, Vigencia, PremioLiquidoVigente, hoje)
            : status.Error;
    }

    public Result<SimulacaoCancelamento> Cancelar(MotivoCancelamento motivo, DateTimeOffset agora)
    {
        var simulacao = SimularCancelamento(motivo, Relogio.HojeEm(agora));
        if (simulacao.IsFailure)
        {
            return simulacao;
        }

        Status = StatusApolice.Cancelada;
        CanceladaEm = agora;
        MotivoCancelamento = motivo;
        ValorRestituicao = simulacao.Value.ValorRestituicao;
        Raise(new ApoliceCancelada(Id, Numero, motivo, simulacao.Value.ValorRestituicao, Segurado.Nome, Segurado.Email.Valor, agora));
        return simulacao;
    }
}
