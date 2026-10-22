using System.Globalization;
using Amazon.SQS;
using Amazon.SQS.Model;
using Apolice.Infrastructure.Configuracao;
using Microsoft.Extensions.Options;

namespace Apolice.Infrastructure.Messaging.Sqs;

// Corpo = payload do evento; metadados em atributos (tipo, traceparent, correlationId) mais o id do outbox, porque o
// MessageId do SQS muda a cada reenvio do relay e o inbox precisa da identidade original.
internal sealed class SqsMessageBus(IAmazonSQS sqs, IOptions<MessagingOptions> opcoes) : IMessageBus
{
    private const string TipoTexto = "String";

    public async Task PublishAsync(MensagemIntegracao mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        using var atividade = TelemetriaMensagens.IniciarPublicacao(mensagem, "aws_sqs");
        var atributos = new Dictionary<string, MessageAttributeValue>
        {
            [CabecalhosMensagem.Tipo] = Texto(mensagem.Tipo),
            [CabecalhosMensagem.MensagemId] = Texto(mensagem.Id.ToString()),
            [CabecalhosMensagem.OcorridoEm] = Texto(mensagem.OcorridoEm.ToString("O", CultureInfo.InvariantCulture)),
        };
        if (mensagem.Traceparent is { } traceparent)
        {
            atributos[CabecalhosMensagem.Traceparent] = Texto(traceparent);
        }

        if (mensagem.CorrelationId is { } correlationId)
        {
            atributos[CabecalhosMensagem.CorrelationId] = Texto(correlationId);
        }

        await sqs.SendMessageAsync(
            new SendMessageRequest { QueueUrl = opcoes.Value.Sqs.QueueUrl, MessageBody = mensagem.Payload, MessageAttributes = atributos },
            cancellationToken);
    }

    private static MessageAttributeValue Texto(string valor) => new() { DataType = TipoTexto, StringValue = valor };
}

// Leitura do envelope do lado da Lambda consumidora (Worker): mesmos nomes de atributo que o publicador escreve.
public static class EnvelopeSqs
{
    public static MensagemIntegracao? Ler(string? corpo, IReadOnlyDictionary<string, string?> atributos)
    {
        ArgumentNullException.ThrowIfNull(atributos);

        var tipo = atributos.GetValueOrDefault(CabecalhosMensagem.Tipo);
        if (corpo is null || string.IsNullOrWhiteSpace(tipo) || !Guid.TryParse(atributos.GetValueOrDefault(CabecalhosMensagem.MensagemId), out var id))
        {
            return null;
        }

        var ocorridoEm = DateTimeOffset.TryParse(atributos.GetValueOrDefault(CabecalhosMensagem.OcorridoEm), CultureInfo.InvariantCulture, DateTimeStyles.None, out var instante)
            ? instante
            : DateTimeOffset.MinValue;
        return new MensagemIntegracao(
            id,
            tipo,
            corpo,
            ocorridoEm,
            atributos.GetValueOrDefault(CabecalhosMensagem.Traceparent),
            atributos.GetValueOrDefault(CabecalhosMensagem.CorrelationId));
    }
}
