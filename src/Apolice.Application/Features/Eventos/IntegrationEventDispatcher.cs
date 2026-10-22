using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Observability;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Eventos;

// Escopo de DI por mensagem (o consumidor cria): cada handler roda, registra o inbox e salva na mesma
// transação. Exceção sobe sem registrar, para o broker reentregar; handlers já concluídos são pulados.
// Tipo conhecido sem consumidor (ex.: TaxaCoberturaAtualizada) é confirmado: não há o que reprocessar.
public sealed partial class IntegrationEventDispatcher(
    IServiceProvider servicos,
    IMensagensProcessadas inbox,
    IUnitOfWork unitOfWork,
    TimeProvider tempo,
    ILogger<IntegrationEventDispatcher> logger) : IIntegrationEventDispatcher
{
    // Montado uma vez: nome da classe do evento ("tipo" do outbox) → tipo concreto + chamada genérica já ligada.
    private static readonly FrozenDictionary<string, Rota> Rotas = MontarRotas();

    public static IReadOnlyCollection<string> TiposConhecidos => Rotas.Keys;

    public async Task DispatchAsync(Guid mensagemId, string tipo, string payload, CancellationToken cancellationToken)
    {
        // Tipo desconhecido não é descartado: confirmar a mensagem a perderia para sempre (ex.: deploy defasado entre a
        // API e o Worker). A exceção a devolve à fila e, esgotadas as entregas, à DLQ, de onde pode ser reenviada.
        if (tipo is null || !Rotas.TryGetValue(tipo, out var rota))
        {
            LogTipoDesconhecido(logger, mensagemId, tipo);
            throw new MensagemDesconhecidaException($"Mensagem {mensagemId} com tipo '{tipo}' desconhecido por esta versão.");
        }

        using var atividade = ApoliceTelemetry.ActivitySource.StartActivity($"{ApoliceTelemetry.AtividadeConsumirEvento} {tipo}");
        atividade?.SetTag(ApoliceTelemetry.TagMensagemId, mensagemId);
        atividade?.SetTag(ApoliceTelemetry.TagTipoMensagem, tipo);

        var evento = JsonSerializer.Deserialize(payload, rota.TipoEvento, JsonOpcoes.Padrao) as IDomainEvent
            ?? throw new InvalidOperationException($"Payload vazio para a mensagem {mensagemId} do tipo {tipo}.");
        await rota.Processar(this, evento, mensagemId, cancellationToken);
    }

    private static FrozenDictionary<string, Rota> MontarRotas()
    {
        var processar = typeof(IntegrationEventDispatcher).GetMethod(nameof(ProcessarAsync), BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Método de processamento do dispatcher não encontrado.");

        return typeof(IDomainEvent).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IDomainEvent)))
            .ToFrozenDictionary(
                t => t.Name,
                t => new Rota(t, processar.MakeGenericMethod(t).CreateDelegate<Func<IntegrationEventDispatcher, IDomainEvent, Guid, CancellationToken, Task>>()),
                StringComparer.Ordinal);
    }

    private async Task ProcessarAsync<TEvento>(IDomainEvent evento, Guid mensagemId, CancellationToken cancellationToken)
        where TEvento : IDomainEvent
    {
        var handlers = servicos.GetServices<IIntegrationEventHandler<TEvento>>().ToList();
        if (handlers.Count == 0)
        {
            LogSemHandler(logger, mensagemId, typeof(TEvento).Name);
            return;
        }

        foreach (var handler in handlers)
        {
            await ExecutarAsync(handler, (TEvento)evento, mensagemId, cancellationToken);
        }
    }

    private async Task ExecutarAsync<TEvento>(IIntegrationEventHandler<TEvento> handler, TEvento evento, Guid mensagemId, CancellationToken cancellationToken)
        where TEvento : IDomainEvent
    {
        var consumidor = handler.GetType().Name;
        if (await inbox.JaProcessadaAsync(mensagemId, consumidor, cancellationToken))
        {
            LogJaProcessada(logger, mensagemId, consumidor);
            return;
        }

        await handler.HandleAsync(evento, cancellationToken);
        inbox.Registrar(mensagemId, consumidor, tempo.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogProcessada(logger, mensagemId, consumidor);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensagem {MensagemId} recusada: tipo '{Tipo}' desconhecido; volta à fila e, esgotadas as entregas, vai para a DLQ.")]
    private static partial void LogTipoDesconhecido(ILogger logger, Guid mensagemId, string? tipo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mensagem {MensagemId} ignorada: nenhum consumidor para {Tipo}.")]
    private static partial void LogSemHandler(ILogger logger, Guid mensagemId, string tipo);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Mensagem {MensagemId} já processada por {Consumidor}; pulando.")]
    private static partial void LogJaProcessada(ILogger logger, Guid mensagemId, string consumidor);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mensagem {MensagemId} processada por {Consumidor}.")]
    private static partial void LogProcessada(ILogger logger, Guid mensagemId, string consumidor);

    private sealed record Rota(Type TipoEvento, Func<IntegrationEventDispatcher, IDomainEvent, Guid, CancellationToken, Task> Processar);
}
