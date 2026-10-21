using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public sealed record SinistroAprovado(
    Guid SinistroId,
    string Numero,
    decimal ValorIndenizacao,
    string SeguradoEmail,
    DateTimeOffset OcorridoEm) : DomainEvent(OcorridoEm);
