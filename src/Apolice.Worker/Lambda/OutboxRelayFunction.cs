using Amazon.Lambda.Core;
using Apolice.Worker.Hospedagem;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Apolice.Worker.Lambda;

public sealed record ResultadoRelay(int Publicadas, bool PrazoEsgotado);

// Handler "Apolice.Worker::Apolice.Worker.Lambda.OutboxRelayFunction::HandleAsync", disparado pela regra agendada do
// EventBridge. O evento é ignorado (Stream: dispensa desserializar um payload que não carrega informação). Esgotar o
// prazo não é erro: o que sobrou fica pendente e a próxima execução, 5 min depois, continua de onde parou.
public sealed partial class OutboxRelayFunction
{
    private readonly Func<CancellationToken, ValueTask<IServiceProvider>> _provedor;

    // Usado pelo runtime da Lambda: o provider é o do container (ProvedorLambda).
    public OutboxRelayFunction()
        : this(ProvedorLambda.ObterAsync)
    {
    }

    public OutboxRelayFunction(IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        _provedor = _ => ValueTask.FromResult(servicos);
    }

    private OutboxRelayFunction(Func<CancellationToken, ValueTask<IServiceProvider>> provedor) => _provedor = provedor;

    public async Task<ResultadoRelay> HandleAsync(Stream evento, ILambdaContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        using var prazo = new CancellationTokenSource(PrazoLambda.Util(contexto.RemainingTime));
        var servicos = await _provedor(prazo.Token);
        var logger = servicos.GetRequiredService<ILogger<OutboxRelayFunction>>();
        try
        {
            var publicadas = await servicos.GetRequiredService<DrenagemOutbox>().DrenarAsync(prazo.Token);
            LogConcluido(logger, publicadas);
            await PurgarAsync(servicos, logger, prazo.Token);
            return new ResultadoRelay(publicadas, prazo.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (prazo.IsCancellationRequested)
        {
            LogPrazoEsgotado(logger);
            return new ResultadoRelay(0, PrazoEsgotado: true);
        }
        finally
        {
            TelemetriaLambda.Descarregar(servicos, contexto.RemainingTime);
        }
    }

    // A execução agendada (a cada 5 min) também faz a manutenção da tabela de idempotência; uma falha aqui não pode
    // apagar o resultado do relay, que já publicou.
    private static async Task PurgarAsync(IServiceProvider servicos, ILogger logger, CancellationToken prazo)
    {
        try
        {
            await servicos.GetRequiredService<PurgaIdempotencia>().PurgarAsync(prazo);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPurgaFalhou(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Relay agendado do outbox: {Publicadas} mensagem(ns) publicada(s).")]
    private static partial void LogConcluido(ILogger logger, int publicadas);

    [LoggerMessage(Level = LogLevel.Error, Message = "Purga das respostas idempotentes vencidas falhou; nova tentativa na próxima execução.")]
    private static partial void LogPurgaFalhou(ILogger logger, Exception excecao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Relay agendado do outbox interrompido pelo prazo da invocação; o restante fica para a próxima execução.")]
    private static partial void LogPrazoEsgotado(ILogger logger);
}
