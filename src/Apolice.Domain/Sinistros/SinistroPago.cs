using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public sealed record SinistroPago(
    Guid SinistroId,
    string Numero,
    string SeguradoEmail,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);
