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
    // O despacho roda dentro da requisição, depois do commit: um broker lento (SQS com o timeout padrão de 100 s e
    // retentativas do SDK; RabbitMQ preso em flow control) seguraria a resposta até o timeout da Lambda, e o cliente
    // repetiria uma operação já gravada. Passado o prazo, o que faltou fica pendente para o relay.
    private static readonly TimeSpan PrazoPublicacao = TimeSpan.FromSeconds(3);

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

    // Sem o token do cliente: o commit já aconteceu e o cancelamento dele não deve interromper a publicação; só o prazo
    // próprio interrompe. A marcação de processado não usa o prazo (publicada e não marcada seria republicada à toa).
    private async Task PublicarAsync(DbContext contexto, List<OutboxMessage> mensagens)
    {
        using var prazo = new CancellationTokenSource(PrazoPublicacao, tempo);
        foreach (var mensagem in mensagens)
        {
            if (prazo.IsCancellationRequested)
            {
                LogPrazoEsgotado(logger, mensagem.Id, mensagem.Tipo);
                continue;
            }

            try
            {
                await bus.PublishAsync(mensagem.ParaMensagem(), prazo.Token);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Despacho imediato da mensagem {MensagemId} ({Tipo}) não coube no prazo; fica para o relay do outbox.")]
    private static partial void LogPrazoEsgotado(ILogger logger, Guid mensagemId, string tipo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Despacho imediato da mensagem {MensagemId} ({Tipo}) falhou; o relay do outbox fará a nova tentativa.")]
    private static partial void LogFalhaDespacho(ILogger logger, Exception excecao, Guid mensagemId, string tipo);
}
