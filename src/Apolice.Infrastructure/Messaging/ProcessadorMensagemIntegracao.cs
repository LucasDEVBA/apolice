using System.Diagnostics;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Observability;
using Apolice.Infrastructure.Correlacao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apolice.Infrastructure.Messaging;

// Lado consumidor comum a todos os transportes (RabbitMQ, SQS na Lambda, InMemory): um escopo de DI por mensagem
// (DbContext e inbox próprios), correlationId no escopo e no log, e o traceparent da mensagem como pai do span de
// consumo — o trace da requisição que gerou o evento continua no Worker. Exceções sobem para o transporte decidir
// entre reentregar e mandar para a DLQ.
public sealed class ProcessadorMensagemIntegracao(IServiceScopeFactory escopos, ILogger<ProcessadorMensagemIntegracao> logger)
{
    public const string AtividadeProcessar = "mensagem.processar";

    public async Task ProcessarAsync(MensagemIntegracao mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        var pai = ActivityContext.TryParse(mensagem.Traceparent, null, out var contexto) ? contexto : default;
        using var atividade = ApoliceTelemetry.ActivitySource.StartActivity($"{AtividadeProcessar} {mensagem.Tipo}", ActivityKind.Consumer, pai);
        atividade?.SetTag(ApoliceTelemetry.TagMensagemId, mensagem.Id);
        atividade?.SetTag(ApoliceTelemetry.TagTipoMensagem, mensagem.Tipo);

        using var escopoLog = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = mensagem.CorrelationId,
            ["MensagemId"] = mensagem.Id,
        });

        await using var escopo = escopos.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoCorrelacao>().Definir(mensagem.CorrelationId);
        var dispatcher = escopo.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        try
        {
            await dispatcher.DispatchAsync(mensagem.Id, mensagem.Tipo, mensagem.Payload, cancellationToken);
        }
        catch (Exception ex)
        {
            atividade?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }
}

// Despacha direto para o dispatcher, sem broker: testes e execução local sem RabbitMQ.
internal sealed class InMemoryMessageBus(ProcessadorMensagemIntegracao processador) : IMessageBus
{
    public Task PublishAsync(MensagemIntegracao mensagem, CancellationToken cancellationToken) =>
        processador.ProcessarAsync(mensagem, cancellationToken);
}

internal static class TelemetriaMensagens
{
    public const string AtividadePublicar = "mensagem.publicar";

    public static Activity? IniciarPublicacao(MensagemIntegracao mensagem, string sistema)
    {
        var atividade = ApoliceTelemetry.ActivitySource.StartActivity($"{AtividadePublicar} {mensagem.Tipo}", ActivityKind.Producer);
        atividade?.SetTag(ApoliceTelemetry.TagMensagemId, mensagem.Id);
        atividade?.SetTag(ApoliceTelemetry.TagTipoMensagem, mensagem.Tipo);
        atividade?.SetTag("messaging.system", sistema);
        return atividade;
    }
}
