using System.Globalization;
using System.Text;
using RabbitMQ.Client;

namespace Apolice.Infrastructure.Messaging.RabbitMq;

// Publica com publisher confirms (o await só termina quando o broker confirma) e mensagens persistentes. Um canal
// dedicado e serializado: canal não é para publicação concorrente, e o volume do outbox não justifica um pool.
internal sealed class RabbitMqMessageBus(ConexaoRabbitMq conexao) : IMessageBus, IAsyncDisposable
{
    private const string TipoConteudo = "application/json";
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IChannel? _canal;

    public async Task PublishAsync(MensagemIntegracao mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        using var atividade = TelemetriaMensagens.IniciarPublicacao(mensagem, "rabbitmq");
        await _trava.WaitAsync(cancellationToken);
        try
        {
            var canal = await CanalAsync(cancellationToken);
            await canal.BasicPublishAsync(
                TopologiaRabbitMq.Exchange,
                routingKey: mensagem.Tipo,
                mandatory: true,
                basicProperties: Propriedades(mensagem),
                body: Encoding.UTF8.GetBytes(mensagem.Payload),
                cancellationToken: cancellationToken);
        }
        finally
        {
            _trava.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is { } canal)
        {
            await canal.DisposeAsync();
        }

        _trava.Dispose();
    }

    // Recria o canal se o anterior fechou (ex.: erro de protocolo); quedas de conexão a recuperação automática resolve.
    private async Task<IChannel> CanalAsync(CancellationToken cancellationToken)
    {
        if (_canal is { IsOpen: true } aberto)
        {
            return aberto;
        }

        var conectado = await conexao.ObterAsync(cancellationToken);
        var canal = await conectado.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await TopologiaRabbitMq.DeclararAsync(canal, cancellationToken);
        _canal = canal;
        return canal;
    }

    private static BasicProperties Propriedades(MensagemIntegracao mensagem)
    {
        var cabecalhos = new Dictionary<string, object?>
        {
            [CabecalhosMensagem.Tipo] = mensagem.Tipo,
            [CabecalhosMensagem.OcorridoEm] = mensagem.OcorridoEm.ToString("O", CultureInfo.InvariantCulture),
        };
        if (mensagem.Traceparent is { } traceparent)
        {
            cabecalhos[CabecalhosMensagem.Traceparent] = traceparent;
        }

        if (mensagem.CorrelationId is { } correlationId)
        {
            cabecalhos[CabecalhosMensagem.CorrelationId] = correlationId;
        }

        return new BasicProperties
        {
            MessageId = mensagem.Id.ToString(),
            Type = mensagem.Tipo,
            ContentType = TipoConteudo,
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = mensagem.CorrelationId,
            Timestamp = new AmqpTimestamp(mensagem.OcorridoEm.ToUnixTimeSeconds()),
            Headers = cabecalhos,
        };
    }
}
