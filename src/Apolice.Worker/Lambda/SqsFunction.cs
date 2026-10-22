using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Apolice.Infrastructure.Messaging;
using Apolice.Infrastructure.Messaging.Sqs;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Apolice.Worker.Lambda;

// Handler "Apolice.Worker::Apolice.Worker.Lambda.SqsFunction::HandleAsync" (event source mapping com
// ReportBatchItemFailures). Cada mensagem é independente: as que falham voltam para a fila sozinhas e, depois de 5
// recebimentos, o redrive as manda para a DLQ — inclusive tipo desconhecido e envelope malformado, que ficam lá para
// reprocessamento manual em vez de serem descartados. Sequencial de propósito: o lote é pequeno (10) e processar em
// paralelo só aumentaria os conflitos de OCC no DSQL.
public sealed partial class SqsFunction
{
    private readonly Func<CancellationToken, ValueTask<IServiceProvider>> _provedor;

    // Usado pelo runtime da Lambda: o provider é o do container (ProvedorLambda).
    public SqsFunction()
        : this(ProvedorLambda.ObterAsync)
    {
    }

    public SqsFunction(IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        _provedor = _ => ValueTask.FromResult(servicos);
    }

    private SqsFunction(Func<CancellationToken, ValueTask<IServiceProvider>> provedor) => _provedor = provedor;

    public async Task<SQSBatchResponse> HandleAsync(SQSEvent evento, ILambdaContext contexto)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(contexto);

        using var prazo = new CancellationTokenSource(PrazoLambda.Util(contexto.RemainingTime));
        var servicos = await _provedor(prazo.Token);
        var processador = servicos.GetRequiredService<ProcessadorMensagemIntegracao>();
        var logger = servicos.GetRequiredService<ILogger<SqsFunction>>();

        var registros = evento.Records ?? [];
        var falhas = new List<SQSBatchResponse.BatchItemFailure>();
        foreach (var registro in registros)
        {
            if (!await ProcessarAsync(registro, processador, logger, prazo.Token))
            {
                falhas.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = registro.MessageId });
            }
        }

        LogLote(logger, registros.Count, falhas.Count);
        TelemetriaLambda.Descarregar(servicos, contexto.RemainingTime);
        return new SQSBatchResponse(falhas);
    }

    private static async Task<bool> ProcessarAsync(SQSEvent.SQSMessage registro, ProcessadorMensagemIntegracao processador, ILogger logger, CancellationToken prazo)
    {
        // Sem tempo para processar: devolver à fila agora custa uma reentrega; tentar custaria o lote inteiro.
        if (prazo.IsCancellationRequested)
        {
            LogSemPrazo(logger, registro.MessageId);
            return false;
        }

        var mensagem = EnvelopeSqs.Ler(registro.Body, Atributos(registro));
        if (mensagem is null)
        {
            LogMalformada(logger, registro.MessageId);
            return false;
        }

        try
        {
            await processador.ProcessarAsync(mensagem, prazo);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFalha(logger, ex, registro.MessageId, mensagem.Id, mensagem.Tipo);
            return false;
        }
    }

    private static Dictionary<string, string?> Atributos(SQSEvent.SQSMessage registro) =>
        registro.MessageAttributes?.ToDictionary(a => a.Key, a => a.Value?.StringValue, StringComparer.Ordinal)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lote SQS: {Total} mensagem(ns), {Falhas} devolvida(s) à fila.")]
    private static partial void LogLote(ILogger logger, int total, int falhas);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mensagem SQS {SqsMessageId} devolvida sem processar: prazo da invocação esgotado.")]
    private static partial void LogSemPrazo(ILogger logger, string sqsMessageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensagem SQS {SqsMessageId} malformada (sem tipo, mensagemId ou corpo); segue para a DLQ após as tentativas.")]
    private static partial void LogMalformada(ILogger logger, string sqsMessageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao processar a mensagem SQS {SqsMessageId} (mensagem {MensagemId}, {Tipo}); devolvida à fila.")]
    private static partial void LogFalha(ILogger logger, Exception excecao, string sqsMessageId, Guid mensagemId, string tipo);
}
