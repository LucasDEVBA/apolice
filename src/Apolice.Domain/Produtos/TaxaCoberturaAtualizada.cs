using Apolice.Domain.Comum;

namespace Apolice.Domain.Produtos;

public sealed record TaxaCoberturaAtualizada(
    string ProdutoCodigo,
    string CoberturaCodigo,
    decimal TaxaAnterior,
    decimal TaxaNova,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);
