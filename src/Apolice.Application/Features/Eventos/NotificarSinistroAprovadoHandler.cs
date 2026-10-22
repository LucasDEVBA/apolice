using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Eventos;

public sealed class NotificarSinistroAprovadoHandler(
    ISinistroRepository sinistros,
    INotificacaoRepository notificacoes,
    TimeProvider tempo) : IIntegrationEventHandler<SinistroAprovado>
{
    public async Task HandleAsync(SinistroAprovado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var sinistro = await sinistros.ObrigatorioAsync(evento.SinistroId, nameof(SinistroAprovado), cancellationToken);
        var modelo = ModelosNotificacao.SinistroAprovado(evento, sinistro);
        notificacoes.Adicionar(FabricaNotificacao.Criar(sinistro.Id, TipoNotificacao.SinistroDecidido, evento.SeguradoEmail, modelo, tempo.GetUtcNow()));
    }
}
