namespace Apolice.Domain.Comum;

public interface IDomainEvent
{
    Guid EventoId { get; }

    DateTimeOffset OcorridoEm { get; }
}
