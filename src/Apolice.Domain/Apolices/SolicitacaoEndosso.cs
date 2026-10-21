namespace Apolice.Domain.Apolices;

public sealed record SolicitacaoEndosso(TipoEndosso Tipo, string CoberturaCodigo, decimal? Lmi = null);
