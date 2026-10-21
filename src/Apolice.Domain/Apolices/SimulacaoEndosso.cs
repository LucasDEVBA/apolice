namespace Apolice.Domain.Apolices;

public sealed record SimulacaoEndosso(
    TipoEndosso Tipo,
    string CoberturaCodigo,
    string CoberturaNome,
    decimal? LmiAnterior,
    decimal? LmiNovo,
    DateOnly DataEfeito,
    int DiasRestantes,
    int DiasVigencia,
    decimal PremioAnualAnterior,
    decimal PremioAnualNovo,
    decimal AjusteLiquido,
    decimal IofAjuste,
    decimal AjusteTotal);
