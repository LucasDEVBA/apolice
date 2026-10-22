using Apolice.Domain.Apolices;

namespace Apolice.Application.Features.Apolices.SimularEndosso;

public sealed record SimulacaoEndossoDto(
    DateOnly DataEfeito,
    int DiasRestantes,
    int DiasVigencia,
    decimal PremioAnualAnterior,
    decimal PremioAnualNovo,
    decimal AjusteLiquido,
    decimal IofAjuste,
    decimal AjusteTotal)
{
    public static SimulacaoEndossoDto De(SimulacaoEndosso simulacao)
    {
        ArgumentNullException.ThrowIfNull(simulacao);
        return new SimulacaoEndossoDto(
            simulacao.DataEfeito,
            simulacao.DiasRestantes,
            simulacao.DiasVigencia,
            simulacao.PremioAnualAnterior,
            simulacao.PremioAnualNovo,
            simulacao.AjusteLiquido,
            simulacao.IofAjuste,
            simulacao.AjusteTotal);
    }
}
