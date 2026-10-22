using Apolice.Infrastructure.Configuracao;
using Apolice.Infrastructure.Outbox;
using Microsoft.Extensions.Options;

namespace Apolice.Worker.Hospedagem;

// Ciclo do relay comum ao host local (OutboxRelayService) e à Lambda agendada (OutboxRelayFunction): lote cheio indica
// backlog, então segue sem esperar o próximo ciclo; lote parcial (ou falhas) encerra a drenagem.
public sealed class DrenagemOutbox(IServiceScopeFactory escopos, IOptions<OutboxOptions> opcoes)
{
    public async Task<int> DrenarAsync(CancellationToken cancellationToken)
    {
        var lote = opcoes.Value.Lote;
        var total = 0;
        int publicadas;
        do
        {
            // Um escopo (DbContext novo) por lote: o change tracker não cresce durante uma drenagem longa.
            await using var escopo = escopos.CreateAsyncScope();
            publicadas = await escopo.ServiceProvider.GetRequiredService<OutboxDispatcher>().DespacharPendentesAsync(lote, cancellationToken);
            total += publicadas;
        }
        while (publicadas == lote && !cancellationToken.IsCancellationRequested);

        return total;
    }
}
