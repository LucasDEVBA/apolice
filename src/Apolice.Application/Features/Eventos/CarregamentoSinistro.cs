using Apolice.Application.Abstractions.Data;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Eventos;

internal static class CarregamentoSinistro
{
    // O sinistro nasce na mesma transação do evento: ausência indica dado inconsistente, não fluxo esperado.
    public static async Task<Sinistro> ObrigatorioAsync(this ISinistroRepository sinistros, Guid sinistroId, string evento, CancellationToken cancellationToken) =>
        await sinistros.ObterAsync(sinistroId, cancellationToken)
            ?? throw new InvalidOperationException($"Sinistro {sinistroId} do evento {evento} não encontrado.");
}
