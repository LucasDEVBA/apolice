using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Apolice.Infrastructure.Messaging.RabbitMq;

// Consumidor da fila apolice.worker que o Worker hospeda: um BackgroundService chama ConsumirAsync, que só termina no
// shutdown. Prefetch 10, um escopo de DI por mensagem (ProcessadorMensagemIntegracao) e ack só depois do commit.
//
// Falha → até 5 tentativas no total, depois DLQ. No RabbitMQ 4 um nack COM requeue não incrementa o delivery-count da
// fila quorum (a mensagem voltaria para sempre), então a nova tentativa é uma REPUBLICAÇÃO com o cabeçalho de
// tentativas incrementado (mesmo MessageId: o inbox continua deduplicando) seguida do ack da original; esgotadas as
// tentativas, nack SEM requeue manda para a DLX/DLQ. O x-delivery-limit da fila segue cobrindo o caso em que o processo
// morre no meio (aí o broker conta a entrega). Mensagem malformada (sem id ou JSON inválido) vai direto para a DLQ.
public sealed partial class RabbitMqConsumidor(ConexaoRabbitMq conexao, ProcessadorMensagemIntegracao processador, ILogger<RabbitMqConsumidor> logger)
{
    public const ushort Prefetch = 10;
    public const string CabecalhoTentativas = "x-apolice-tentativas";
    private const string CabecalhoContagemEntregas = "x-delivery-count";
    private const int EsperaBaseMs = 200;
    private const int EsperaMaximaMs = 5_000;
    private static readonly TimeSpan PausaReconexao = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VerificacaoCanal = TimeSpan.FromSeconds(2);

    // Só termina no cancelamento. Falha ao conectar (broker ainda subindo) ou canal fechado pelo broker (erro de
    // protocolo) recomeçam o ciclo depois de uma pausa; queda de conexão a recuperação automática do cliente resolve.
    public async Task ConsumirAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirEnquantoCanalAbertoAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogConsumoInterrompido(logger, ex, PausaReconexao.TotalSeconds);
            }

            try
            {
                await Task.Delay(PausaReconexao, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task ConsumirEnquantoCanalAbertoAsync(CancellationToken cancellationToken)
    {
        var conectado = await conexao.ObterAsync(cancellationToken);
        await using var canal = await conectado.CreateChannelAsync(cancellationToken: cancellationToken);
        await using var republicacao = await conectado.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await TopologiaRabbitMq.DeclararAsync(canal, cancellationToken);
        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: Prefetch, global: false, cancellationToken);

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += (_, entrega) => TratarAsync(canal, republicacao, entrega, cancellationToken);
        var tag = await canal.BasicConsumeAsync(TopologiaRabbitMq.Fila, autoAck: false, consumidor, cancellationToken);
        LogConsumindo(logger, TopologiaRabbitMq.Fila, Prefetch);
        try
        {
            // Conexão caída (IsOpen falso) está em recuperação automática: espera. Canal fechado com a conexão de pé
            // não volta sozinho: sai para recriar.
            while (canal.IsOpen || !conectado.IsOpen)
            {
                await Task.Delay(VerificacaoCanal, cancellationToken);
            }
        }
        finally
        {
            if (canal.IsOpen)
            {
                await canal.BasicCancelAsync(tag, noWait: false, CancellationToken.None);
            }
        }
    }

    private async Task TratarAsync(IChannel canal, IChannel republicacao, BasicDeliverEventArgs entrega, CancellationToken cancellationToken)
    {
        var corpo = entrega.Body.ToArray();
        var mensagem = Ler(entrega.BasicProperties, corpo);
        if (mensagem is null)
        {
            LogMalformada(logger, entrega.BasicProperties.MessageId);
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        try
        {
            await processador.ProcessarAsync(mensagem, cancellationToken);
            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await TratarFalhaAsync(canal, republicacao, entrega, corpo, mensagem, ex);
        }
    }

    private async Task TratarFalhaAsync(IChannel canal, IChannel republicacao, BasicDeliverEventArgs entrega, byte[] corpo, MensagemIntegracao mensagem, Exception ex)
    {
        var tentativa = Tentativas(entrega.BasicProperties) + 1;
        var definitiva = ex is JsonException || tentativa >= TopologiaRabbitMq.LimiteEntregas;
        LogFalha(logger, ex, mensagem.Id, mensagem.Tipo, tentativa, definitiva);
        if (definitiva)
        {
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        // Espera crescente antes da nova tentativa: um conflito de concorrência tem chance de não se repetir.
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(EsperaMaximaMs, EsperaBaseMs * (1L << tentativa))), CancellationToken.None);
        try
        {
            await republicacao.BasicPublishAsync(
                TopologiaRabbitMq.Exchange,
                entrega.RoutingKey,
                mandatory: true,
                basicProperties: ComTentativa(entrega.BasicProperties, tentativa),
                body: corpo,
                cancellationToken: CancellationToken.None);
            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, CancellationToken.None);
        }
        catch (Exception falhaRepublicacao) when (falhaRepublicacao is not OutOfMemoryException)
        {
            // Sem como republicar, devolve a original: nada se perde (o broker a entrega de novo).
            LogRepublicacaoFalhou(logger, falhaRepublicacao, mensagem.Id);
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
        }
    }

    internal static MensagemIntegracao? Ler(IReadOnlyBasicProperties propriedades, byte[] corpo)
    {
        var tipo = propriedades.Type ?? Cabecalho(propriedades.Headers, CabecalhosMensagem.Tipo);
        if (!Guid.TryParse(propriedades.MessageId, out var id) || string.IsNullOrWhiteSpace(tipo))
        {
            return null;
        }

        var ocorridoEm = DateTimeOffset.TryParse(Cabecalho(propriedades.Headers, CabecalhosMensagem.OcorridoEm), CultureInfo.InvariantCulture, DateTimeStyles.None, out var instante)
            ? instante
            : DateTimeOffset.FromUnixTimeSeconds(propriedades.Timestamp.UnixTime);
        return new MensagemIntegracao(
            id,
            tipo,
            Encoding.UTF8.GetString(corpo),
            ocorridoEm,
            Cabecalho(propriedades.Headers, CabecalhosMensagem.Traceparent),
            Cabecalho(propriedades.Headers, CabecalhosMensagem.CorrelationId) ?? propriedades.CorrelationId);
    }

    private static BasicProperties ComTentativa(IReadOnlyBasicProperties original, int tentativa)
    {
        var copia = new BasicProperties(original);
        var cabecalhos = new Dictionary<string, object?>(original.Headers ?? new Dictionary<string, object?>(), StringComparer.Ordinal)
        {
            [CabecalhoTentativas] = tentativa,
        };
        cabecalhos.Remove(CabecalhoContagemEntregas);
        copia.Headers = cabecalhos;
        return copia;
    }

    // O maior entre o que contamos (republicações) e o que o broker contou (entregas devolvidas por queda do consumidor).
    private static int Tentativas(IReadOnlyBasicProperties propriedades) =>
        Math.Max(Inteiro(propriedades.Headers, CabecalhoTentativas), Inteiro(propriedades.Headers, CabecalhoContagemEntregas));

    private static int Inteiro(IDictionary<string, object?>? cabecalhos, string nome) =>
        cabecalhos is not null && cabecalhos.TryGetValue(nome, out var valor) && valor is IConvertible convertivel
            ? convertivel.ToInt32(CultureInfo.InvariantCulture)
            : 0;

    // O cliente entrega cabeçalhos de texto como byte[] (AMQP longstr).
    private static string? Cabecalho(IDictionary<string, object?>? cabecalhos, string nome) =>
        cabecalhos is not null && cabecalhos.TryGetValue(nome, out var valor)
            ? valor switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string texto => texto,
                _ => null,
            }
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumo do RabbitMQ interrompido; nova tentativa em {Segundos} s.")]
    private static partial void LogConsumoInterrompido(ILogger logger, Exception excecao, double segundos);

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumindo a fila {Fila} (prefetch {Prefetch}).")]
    private static partial void LogConsumindo(ILogger logger, string fila, ushort prefetch);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensagem malformada (MessageId '{MessageId}') enviada para a DLQ.")]
    private static partial void LogMalformada(ILogger logger, string? messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao processar a mensagem {MensagemId} ({Tipo}) na tentativa {Tentativa}; definitiva (DLQ): {Definitiva}.")]
    private static partial void LogFalha(ILogger logger, Exception excecao, Guid mensagemId, string tipo, int tentativa, bool definitiva);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível republicar a mensagem {MensagemId} para nova tentativa; devolvida à fila.")]
    private static partial void LogRepublicacaoFalhou(ILogger logger, Exception excecao, Guid mensagemId);
}
