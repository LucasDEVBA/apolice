using System.Text.Json;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Infrastructure.Messaging;

namespace Apolice.Infrastructure.Outbox;

// Linha do outbox transacional: gravada no mesmo SaveChanges do agregado que levantou o evento.
// O Id é o EventoId do domínio — a mesma chave de idempotência que o inbox do consumidor usa.
public sealed class OutboxMessage
{
    public const int TamanhoMaximoTipo = 100;
    public const int TamanhoMaximoTraceparent = 128;
    public const int TamanhoMaximoErro = 2_000;

    private OutboxMessage()
    {
        Tipo = null!;
        Payload = null!;
    }

    private OutboxMessage(Guid id, string tipo, string payload, DateTimeOffset ocorridoEm, string? traceparent, string? correlationId)
    {
        Id = id;
        Tipo = tipo;
        Payload = payload;
        OcorridoEm = ocorridoEm;
        Traceparent = traceparent;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }

    public string Tipo { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }

    public string? Traceparent { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset? ProcessadoEm { get; private set; }

    public int Tentativas { get; private set; }

    public string? UltimoErro { get; private set; }

    // Serializa pelo tipo concreto: pelo tipo declarado (IDomainEvent) o JSON sairia só com EventoId e OcorridoEm.
    public static OutboxMessage De(IDomainEvent evento, string? traceparent, string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var payload = JsonSerializer.Serialize(evento, evento.GetType(), JsonOpcoes.Padrao);
        var traceparentValido = traceparent is { Length: <= TamanhoMaximoTraceparent } ? traceparent : null;
        return new OutboxMessage(evento.EventoId, evento.GetType().Name, payload, evento.OcorridoEm, traceparentValido, correlationId);
    }

    public MensagemIntegracao ParaMensagem() => new(Id, Tipo, Payload, OcorridoEm, Traceparent, CorrelationId);
}
