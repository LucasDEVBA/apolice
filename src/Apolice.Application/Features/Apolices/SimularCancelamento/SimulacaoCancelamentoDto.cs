using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices.SimularCancelamento;

// PercentualRetido é fração (0,13 = 13%), como no domínio; só vem no método PrazoCurto.
public sealed record SimulacaoCancelamentoDto(
    DateOnly DataCancelamento,
    int DiasDecorridos,
    int DiasVigencia,
    MetodoCancelamento Metodo,
    decimal? PercentualRetido,
    decimal PremioLiquidoBase,
    decimal ValorRestituicao)
{
    public static SimulacaoCancelamentoDto De(SimulacaoCancelamento simulacao)
    {
        ArgumentNullException.ThrowIfNull(simulacao);
        return new SimulacaoCancelamentoDto(
            simulacao.DataCancelamento,
            simulacao.DiasDecorridos,
            simulacao.DiasVigencia,
            simulacao.Metodo,
            simulacao.PercentualRetido,
            simulacao.PremioLiquidoBase,
            simulacao.ValorRestituicao);
    }
}
