using System.Globalization;
using Amazon.Lambda.SQSEvents;
using Apolice.Infrastructure.Messaging;
using Apolice.Infrastructure.Outbox;

namespace Apolice.Api.IntegrationTests.Worker.Suporte;

// Registros SQS como o event source mapping os entrega: corpo = payload, metadados nos atributos que o SqsMessageBus
// escreve. Cada chamada gera um MessageId novo do SQS, como numa reentrega real (o mensagemId do outbox se mantém).
internal static class EventosSqs
{
    private const string TipoTexto = "String";

    public static SQSEvent Lote(params SQSEvent.SQSMessage[] registros) => new() { Records = [.. registros] };

    public static SQSEvent.SQSMessage De(OutboxMessage mensagem) =>
        Registro(mensagem.Id, mensagem.Tipo, mensagem.Payload, mensagem.OcorridoEm, mensagem.Traceparent, mensagem.CorrelationId);

    public static SQSEvent.SQSMessage Registro(Guid mensagemId, string tipo, string corpo, DateTimeOffset? ocorridoEm = null, string? traceparent = null, string? correlationId = null)
    {
        var atributos = new Dictionary<string, SQSEvent.MessageAttribute>(StringComparer.Ordinal)
        {
            [CabecalhosMensagem.Tipo] = Texto(tipo),
            [CabecalhosMensagem.MensagemId] = Texto(mensagemId.ToString()),
            [CabecalhosMensagem.OcorridoEm] = Texto((ocorridoEm ?? DateTimeOffset.UtcNow).ToString("O", CultureInfo.InvariantCulture)),
        };
        if (traceparent is not null)
        {
            atributos[CabecalhosMensagem.Traceparent] = Texto(traceparent);
        }

        if (correlationId is not null)
        {
            atributos[CabecalhosMensagem.CorrelationId] = Texto(correlationId);
        }

        return new SQSEvent.SQSMessage { MessageId = Guid.NewGuid().ToString(), Body = corpo, MessageAttributes = atributos };
    }

    // Sem os atributos do envelope (ex.: mensagem enviada à mão pelo console da AWS).
    public static SQSEvent.SQSMessage SemEnvelope(string corpo) =>
        new() { MessageId = Guid.NewGuid().ToString(), Body = corpo, MessageAttributes = new Dictionary<string, SQSEvent.MessageAttribute>(StringComparer.Ordinal) };

    private static SQSEvent.MessageAttribute Texto(string valor) => new() { DataType = TipoTexto, StringValue = valor };
}
