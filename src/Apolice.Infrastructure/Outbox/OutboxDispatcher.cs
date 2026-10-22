using Apolice.Infrastructure.Messaging;
using Apolice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apolice.Infrastructure.Outbox;

// Relay do outbox (Worker local a cada Outbox:IntervaloSegundos; Lambda agendada em produção): publica as pendentes
// em ordem de ocorrência. Cada mensagem é marcada sozinha (ExecuteUpdate), sem transação longa: uma falha no meio do
// lote não desfaz o que já saiu. Publicar duas vezes (relay + despacho imediato ao mesmo tempo) é aceitável — o
// inbox do consumidor deduplica pelo id.
public sealed partial class OutboxDispatcher(ApoliceDbContext db, IMessageBus bus, TimeProvider tempo, ILogger<OutboxDispatcher> logger)
{
    // Devolve quantas foram publicadas, para o host decidir se já esvaziou o backlog.
    public async Task<int> DespacharPendentesAsync(int lote, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lote, 1);

        var pendentes = await db.Outbox.AsNoTracking()
            .Where(m => m.ProcessadoEm == null)
            .OrderBy(m => m.OcorridoEm)
            .ThenBy(m => m.Id)
            .Take(lote)
            .ToListAsync(cancellationToken);

        var publicadas = 0;
        foreach (var mensagem in pendentes)
        {
            if (await PublicarAsync(mensagem, cancellationToken))
            {
                publicadas++;
            }
        }

        if (pendentes.Count > 0)
        {
            LogLote(logger, publicadas, pendentes.Count);
        }

        return publicadas;
    }

    private async Task<bool> PublicarAsync(OutboxMessage mensagem, CancellationToken cancellationToken)
    {
        try
        {
            await bus.PublishAsync(mensagem.ParaMensagem(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFalha(logger, ex, mensagem.Id, mensagem.Tipo, mensagem.Tentativas + 1);
            var erro = Resumir(ex);
            await db.Outbox.Where(m => m.Id == mensagem.Id).ExecuteUpdateAsync(
                s => s.SetProperty(m => m.Tentativas, m => m.Tentativas + 1).SetProperty(m => m.UltimoErro, erro),
                cancellationToken);
            return false;
        }

        // Publicada: a marcação não usa o token do chamador, senão um cancelamento agora faria republicar no próximo ciclo.
        var agora = tempo.GetUtcNow();
        await db.Outbox.Where(m => m.Id == mensagem.Id && m.ProcessadoEm == null).ExecuteUpdateAsync(
            s => s.SetProperty(m => m.ProcessadoEm, agora).SetProperty(m => m.Tentativas, m => m.Tentativas + 1),
            CancellationToken.None);
        return true;
    }

    private static string Resumir(Exception ex)
    {
        var texto = $"{ex.GetType().Name}: {ex.Message}";
        return texto.Length > OutboxMessage.TamanhoMaximoErro ? texto[..OutboxMessage.TamanhoMaximoErro] : texto;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Relay do outbox: {Publicadas} de {Total} mensagens publicadas.")]
    private static partial void LogLote(ILogger logger, int publicadas, int total);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Relay do outbox: falha ao publicar {MensagemId} ({Tipo}), tentativa {Tentativa}.")]
    private static partial void LogFalha(ILogger logger, Exception excecao, Guid mensagemId, string tipo, int tentativa);
}
