using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Eventos;

public sealed class NotificarSinistroNegadoHandler(
    ISinistroRepository sinistros,
    INotificacaoRepository notificacoes,
    TimeProvider tempo) : IIntegrationEventHandler<SinistroNegado>
{
    public async Task HandleAsync(SinistroNegado evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var sinistro = await sinistros.ObrigatorioAsync(evento.SinistroId, nameof(SinistroNegado), cancellationToken);
        var modelo = ModelosNotificacao.SinistroNegado(evento, sinistro);
        notificacoes.Adicionar(FabricaNotificacao.Criar(sinistro.Id, TipoNotificacao.SinistroDecidido, evento.SeguradoEmail, modelo, tempo.GetUtcNow()));
    }
}
