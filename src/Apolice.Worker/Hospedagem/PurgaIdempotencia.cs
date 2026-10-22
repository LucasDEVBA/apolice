using Apolice.Infrastructure.Idempotencia;

namespace Apolice.Worker.Hospedagem;

// SPEC 7: a resposta idempotente vale 24 h, e o corpo gravado traz CPF e e-mail do segurado; sem a purga ficaria retido
// para sempre (a linha só era trocada se a mesma chave voltasse). Lotes pequenos com teto por execução: o DSQL limita
// as linhas alteradas por transação, e o que sobrar sai na execução seguinte.
public sealed partial class PurgaIdempotencia(IArmazemIdempotencia armazem, TimeProvider tempo, ILogger<PurgaIdempotencia> logger)
{
    private const int Lote = 500;
    private const int LotesPorExecucao = 20;

    public async Task<int> PurgarAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        for (var lote = 0; lote < LotesPorExecucao; lote++)
        {
            var apagados = await armazem.PurgarVencidosAsync(tempo.GetUtcNow(), Lote, cancellationToken);
            total += apagados;
            if (apagados < Lote)
            {
                break;
            }
        }

        if (total > 0)
        {
            LogPurga(logger, total);
        }

        return total;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Idempotência: {Total} resposta(s) vencida(s) apagada(s).")]
    private static partial void LogPurga(ILogger logger, int total);
}
