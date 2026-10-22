using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Services;
using Apolice.Domain.Apolices;
using Apolice.Domain.Notificacoes;
using Microsoft.Extensions.Logging;

namespace Apolice.Application.Features.Eventos;

// Grava na apólice (versionada) logo depois da emissão: um endosso feito nesse mesmo instante pode receber 409
// (Concorrencia.Conflito) e é só refeito pelo usuário; do lado de cá, o conflito devolve a mensagem à fila e a
// reentrega é idempotente (inbox + BoasVindasEnviadasEm).
public sealed partial class BoasVindasApoliceEmitidaHandler(
    IApoliceRepository apolices,
    INotificacaoRepository notificacoes,
    ICatalogoProdutos catalogo,
    TimeProvider tempo,
    ILogger<BoasVindasApoliceEmitidaHandler> logger) : IIntegrationEventHandler<ApoliceEmitida>
{
    public async Task HandleAsync(ApoliceEmitida evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var apolice = await apolices.ObterAsync(evento.ApoliceId, cancellationToken)
            ?? throw new InvalidOperationException($"Apólice {evento.ApoliceId} do evento ApoliceEmitida não encontrada.");

        // Segunda barreira além do inbox: um e-mail de boas-vindas já marcado nunca é reenviado.
        if (apolice.BoasVindasEnviadasEm is not null)
        {
            LogJaEnviada(logger, apolice.Id);
            return;
        }

        var produto = await catalogo.ObterAsync(evento.ProdutoCodigo, cancellationToken);
        var agora = tempo.GetUtcNow();
        var modelo = ModelosNotificacao.BoasVindas(evento, produto?.Nome ?? evento.ProdutoCodigo);
        notificacoes.Adicionar(FabricaNotificacao.Criar(apolice.Id, TipoNotificacao.BoasVindas, evento.SeguradoEmail, modelo, agora));
        apolice.MarcarBoasVindasEnviadas(agora);
        LogEnviada(logger, apolice.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Boas-vindas da apólice {ApoliceId} geradas.")]
    private static partial void LogEnviada(ILogger logger, Guid apoliceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Boas-vindas da apólice {ApoliceId} já haviam sido enviadas; nada a fazer.")]
    private static partial void LogJaEnviada(ILogger logger, Guid apoliceId);
}
