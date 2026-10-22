using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Eventos;

// O enum de notificações não tem "pago": o pagamento é o desfecho da decisão (SinistroDecidido).
public sealed class NotificarSinistroPagoHandler(
    ISinistroRepository sinistros,
    INotificacaoRepository notificacoes,
    TimeProvider tempo) : IIntegrationEventHandler<SinistroPago>
{
    public async Task HandleAsync(SinistroPago evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var sinistro = await sinistros.ObrigatorioAsync(evento.SinistroId, nameof(SinistroPago), cancellationToken);
        var modelo = ModelosNotificacao.SinistroPago(evento, sinistro);
        notificacoes.Adicionar(FabricaNotificacao.Criar(sinistro.Id, TipoNotificacao.SinistroDecidido, evento.SeguradoEmail, modelo, tempo.GetUtcNow()));
    }
}
