using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public sealed record SinistroNegado(
    Guid SinistroId,
    string Numero,
    string Motivo,
    string SeguradoEmail,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);
