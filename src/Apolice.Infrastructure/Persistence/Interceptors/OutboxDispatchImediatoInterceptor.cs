using Apolice.Infrastructure.Messaging;
using Apolice.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Apolice.Infrastructure.Persistence.Interceptors;

// Despacho imediato (Outbox:DispatchImediato): logo depois do commit publica as mensagens que este SaveChanges gravou,
// para o consumidor não esperar o próximo ciclo do relay. É best-effort: qualquer falha só é registrada em log e a
// mensagem continua pendente no outbox, onde o relay a encontra. Uma instância por DbContext (estado por save).
internal sealed partial class OutboxDispatchImediatoInterceptor(
    IMessageBus bus,
    TimeProvider tempo,
    ILogger<OutboxDispatchImediatoInterceptor> logger) : SaveChangesInterceptor
{
    private List<OutboxMessage> _gravadas = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Capturar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Capturar(eventData.Context);
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var pendentes = Interlocked.Exchange(ref _gravadas, []);
        if (eventData.Context is { } contexto && pendentes.Count > 0)
        {
            await PublicarAsync(contexto, pendentes);
        }

        return result;
    }

    // Caminho síncrono não publica: bloquear a thread no broker seria pior que esperar o relay.
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        _gravadas = [];
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _gravadas = [];

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _gravadas = [];
        return Task.CompletedTask;
    }

    // Dentro de uma transação explícita o SaveChanges ainda não é o commit: publicar agora poderia anunciar algo que
    // será desfeito. Nesse caso o relay publica depois.
    private void Capturar(DbContext? contexto) =>
        _gravadas = contexto is null || contexto.Database.CurrentTransaction is not null
            ? []
            : [.. contexto.ChangeTracker.Entries<OutboxMessage>().Where(e => e.State == EntityState.Added).Select(e => e.Entity)];

    // CancellationToken.None: o commit já aconteceu; o cancelamento do cliente não deve interromper a publicação.
    private async Task PublicarAsync(DbContext contexto, List<OutboxMessage> mensagens)
    {
        foreach (var mensagem in mensagens)
        {
            try
            {
                await bus.PublishAsync(mensagem.ParaMensagem(), CancellationToken.None);
                var agora = tempo.GetUtcNow();
                await contexto.Set<OutboxMessage>()
                    .Where(m => m.Id == mensagem.Id && m.ProcessadoEm == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessadoEm, agora), CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogFalhaDespacho(logger, ex, mensagem.Id, mensagem.Tipo);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Despacho imediato da mensagem {MensagemId} ({Tipo}) falhou; o relay do outbox fará a nova tentativa.")]
    private static partial void LogFalhaDespacho(ILogger logger, Exception excecao, Guid mensagemId, string tipo);
}
