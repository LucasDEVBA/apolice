using Apolice.Domain.Comum;

namespace Apolice.Domain.Apolices;

public sealed record ApoliceEndossada(
    Guid ApoliceId,
    string Numero,
    int EndossoNumero,
    TipoEndosso Tipo,
    string CoberturaCodigo,
    decimal AjusteTotal,
    string SeguradoNome,
    string SeguradoEmail,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);
