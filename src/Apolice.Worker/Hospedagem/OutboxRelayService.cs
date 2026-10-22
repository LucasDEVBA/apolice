using Apolice.Infrastructure.Configuracao;
using Microsoft.Extensions.Options;

namespace Apolice.Worker.Hospedagem;

// Relay local a cada Outbox:IntervaloSegundos. Uma falha (banco ou broker fora) é registrada e o ciclo seguinte tenta
// de novo: derrubar o host por isso só trocaria o problema por um restart em loop.
public sealed partial class OutboxRelayService(
    DrenagemOutbox drenagem,
    PurgaIdempotencia purga,
    IOptions<OutboxOptions> opcoes,
    TimeProvider tempo,
    ILogger<OutboxRelayService> logger) : BackgroundService
{
    // A purga não precisa do ritmo do relay (segundos): os registros vencem em horas.
    private static readonly TimeSpan IntervaloPurga = TimeSpan.FromMinutes(10);
    private DateTimeOffset _proximaPurga = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(opcoes.Value.IntervaloSegundos);
        LogIniciado(logger, intervalo.TotalSeconds, opcoes.Value.Lote);

        using var temporizador = new PeriodicTimer(intervalo, tempo);
        do
        {
            await CicloAsync(stoppingToken);
        }
        while (await AguardarProximoCicloAsync(temporizador, stoppingToken));
    }

    private async Task CicloAsync(CancellationToken stoppingToken)
    {
        try
        {
            await drenagem.DrenarAsync(stoppingToken);
            await PurgarSeDevidoAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogInterrompido(logger);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogCicloFalhou(logger, ex);
        }
    }

    private async Task PurgarSeDevidoAsync(CancellationToken stoppingToken)
    {
        var agora = tempo.GetUtcNow();
        if (agora < _proximaPurga)
        {
            return;
        }

        _proximaPurga = agora + IntervaloPurga;
        await purga.PurgarAsync(stoppingToken);
    }

    private static async Task<bool> AguardarProximoCicloAsync(PeriodicTimer temporizador, CancellationToken stoppingToken)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Relay do outbox iniciado: a cada {Segundos} s, lotes de {Lote}.")]
    private static partial void LogIniciado(ILogger logger, double segundos, int lote);

    [LoggerMessage(Level = LogLevel.Information, Message = "Relay do outbox interrompido pelo encerramento do host.")]
    private static partial void LogInterrompido(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ciclo do relay do outbox falhou; nova tentativa no próximo ciclo.")]
    private static partial void LogCicloFalhou(ILogger logger, Exception excecao);
}
