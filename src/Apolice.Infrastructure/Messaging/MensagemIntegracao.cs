namespace Apolice.Infrastructure.Messaging;

// Envelope transportado pelos brokers: o payload é o JSON do evento de domínio (JsonOpcoes.Padrao) e o tipo é o
// nome da classe do evento, como o IIntegrationEventDispatcher espera.
public sealed record MensagemIntegracao(
    Guid Id,
    string Tipo,
    string Payload,
    DateTimeOffset OcorridoEm,
    string? Traceparent,
    string? CorrelationId);

public interface IMessageBus
{
    // Retorna só depois de o broker confirmar a mensagem (publisher confirm / resposta do SendMessage): o outbox só
    // marca como processada o que de fato saiu.
    Task PublishAsync(MensagemIntegracao mensagem, CancellationToken cancellationToken);
}

// Nomes dos cabeçalhos (RabbitMQ) e atributos (SQS): o consumidor de um transporte lê o que o outro escreveria.
public static class CabecalhosMensagem
{
    public const string Tipo = "tipo";
    public const string Traceparent = "traceparent";
    public const string CorrelationId = "correlationId";

    // SQS gera o próprio MessageId: o id do outbox viaja à parte para o inbox deduplicar as reentregas do relay.
    public const string MensagemId = "mensagemId";
    public const string OcorridoEm = "ocorridoEm";
}
