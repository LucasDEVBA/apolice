using RabbitMQ.Client;

namespace Apolice.Infrastructure.Messaging.RabbitMq;

// Topologia do SPEC 6, declarada de forma idempotente por quem publica e por quem consome (o que subir primeiro
// cria). Fila quorum com x-delivery-limit: depois de 5 entregas sem ack a mensagem vai para a DLX/DLQ sozinha.
public static class TopologiaRabbitMq
{
    public const string Exchange = "apolice.eventos";
    public const string Fila = "apolice.worker";
    public const string ExchangeDlx = "apolice.eventos.dlx";
    public const string FilaDlq = "apolice.worker.dlq";
    public const int LimiteEntregas = 5;
    private const string TodasAsChaves = "#";

    public static async Task DeclararAsync(IChannel canal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canal);

        await canal.ExchangeDeclareAsync(ExchangeDlx, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await canal.QueueDeclareAsync(
            FilaDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);
        await canal.QueueBindAsync(FilaDlq, ExchangeDlx, TodasAsChaves, cancellationToken: cancellationToken);

        await canal.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await canal.QueueDeclareAsync(
            Fila,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = LimiteEntregas,
                ["x-dead-letter-exchange"] = ExchangeDlx,
            },
            cancellationToken: cancellationToken);
        await canal.QueueBindAsync(Fila, Exchange, TodasAsChaves, cancellationToken: cancellationToken);
    }
}
