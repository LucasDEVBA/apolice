using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public sealed record SinistroAberto(
    Guid SinistroId,
    string Numero,
    Guid ApoliceId,
    string CoberturaCodigo,
    decimal ValorReclamado,
    string SeguradoEmail,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);
