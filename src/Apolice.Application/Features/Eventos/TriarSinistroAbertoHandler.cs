using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Sinistros;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Eventos;

// Grava no Sinistro (versionado) logo depois do commit de quem o abriu: uma análise iniciada nesse mesmo instante pode
// receber 409 (Concorrencia.Conflito) e é só refeita pelo usuário; do lado de cá, o conflito devolve a mensagem à fila
// e a reentrega é idempotente (inbox + "só a primeira triagem vale").
public sealed partial class TriarSinistroAbertoHandler(
    IFeatureFlags flags,
    ISinistroRepository sinistros,
    IApoliceRepository apolices,
    IAuditoria auditoria,
    TimeProvider tempo,
    ILogger<TriarSinistroAbertoHandler> logger) : IIntegrationEventHandler<SinistroAberto>
{
    public async Task HandleAsync(SinistroAberto evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        // Flag desligada: a mensagem é dada como processada (sem triagem); religar não triará retroativamente.
        if (!await flags.IsEnabledAsync(FeatureFlags.TriagemAutomaticaSinistros, cancellationToken))
        {
            LogTriagemDesligada(logger, evento.SinistroId);
            return;
        }

        var sinistro = await sinistros.ObrigatorioAsync(evento.SinistroId, nameof(SinistroAberto), cancellationToken);
        if (sinistro.Triagem is not null)
        {
            return;
        }

        var apolice = await apolices.ObterAsync(sinistro.ApoliceId, cancellationToken)
            ?? throw new InvalidOperationException($"Apólice {sinistro.ApoliceId} do sinistro {sinistro.Id} não encontrada.");
        var agora = tempo.GetUtcNow();
        var triagem = AvaliadorTriagem.Avaliar(sinistro, apolice.Vigencia.Inicio, Relogio.HojeEm(agora));
        sinistro.RegistrarTriagem(triagem, agora);
        auditoria.Registrar(
            AcoesAuditoria.SinistroTriado,
            EntidadesAuditoria.Sinistro,
            sinistro.Id.ToString(),
            DetalhesAuditoria.Json(new { score = triagem.Score, alertas = triagem.Alertas }));
        LogTriado(logger, sinistro.Id, triagem.Score);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Triagem automática desligada; sinistro {SinistroId} segue sem score.")]
    private static partial void LogTriagemDesligada(ILogger logger, Guid sinistroId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sinistro {SinistroId} triado com score {Score}.")]
    private static partial void LogTriado(ILogger logger, Guid sinistroId, int score);
}
