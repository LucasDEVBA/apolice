using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Apolices;
using Apolice.Domain.Notificacoes;

namespace Apolice.Application.Features.Eventos;

public sealed class NotificarApoliceCanceladaHandler(INotificacaoRepository notificacoes, TimeProvider tempo)
    : IIntegrationEventHandler<ApoliceCancelada>
{
    public Task HandleAsync(ApoliceCancelada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var modelo = ModelosNotificacao.Cancelamento(evento);
        notificacoes.Adicionar(FabricaNotificacao.Criar(evento.ApoliceId, TipoNotificacao.Cancelamento, evento.SeguradoEmail, modelo, tempo.GetUtcNow()));
        return Task.CompletedTask;
    }
}
