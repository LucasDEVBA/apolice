using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Eventos;

public sealed class NotificarSinistroAbertoHandler(
    ISinistroRepository sinistros,
    INotificacaoRepository notificacoes,
    TimeProvider tempo) : IIntegrationEventHandler<SinistroAberto>
{
    public async Task HandleAsync(SinistroAberto evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var sinistro = await sinistros.ObrigatorioAsync(evento.SinistroId, nameof(SinistroAberto), cancellationToken);
        var modelo = ModelosNotificacao.SinistroAberto(sinistro);
        notificacoes.Adicionar(FabricaNotificacao.Criar(sinistro.Id, TipoNotificacao.SinistroAberto, evento.SeguradoEmail, modelo, tempo.GetUtcNow()));
    }
}
