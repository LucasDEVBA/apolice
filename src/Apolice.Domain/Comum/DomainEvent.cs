using System.Text.Json.Serialization;

namespace Apolice.Domain.Comum;

// Base comum para que nenhum evento concreto esqueça o OcorridoEm (o tempo vem sempre do chamador)
// e para que o EventoId siga a mesma linha do tempo de negócio (v7 ancorado em OcorridoEm).
public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent(DateTimeOffset ocorridoEm)
    {
        OcorridoEm = ocorridoEm;
        EventoId = Guid.CreateVersion7(ocorridoEm);
    }

    // O consumidor precisa desserializar preservando o Id original (chave de idempotência do outbox/inbox);
    // JsonInclude com init privado permite isso sem deixar outra camada trocar a identidade com "with".
    [JsonInclude]
    public Guid EventoId { get; private init; }

    public DateTimeOffset OcorridoEm { get; }
}
