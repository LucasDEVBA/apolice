using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Messaging;
using Apolice.Domain.Apolices;
using Apolice.Domain.Notificacoes;

namespace Apolice.Application.Features.Eventos;

public sealed class NotificarApoliceEndossadaHandler(
    IApoliceRepository apolices,
    INotificacaoRepository notificacoes,
    TimeProvider tempo) : IIntegrationEventHandler<ApoliceEndossada>
{
    public async Task HandleAsync(ApoliceEndossada evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        // O evento só traz o código; o nome da cobertura e a data de efeito vêm do endosso gravado.
        var apolice = await apolices.ObterAsync(evento.ApoliceId, cancellationToken)
            ?? throw new InvalidOperationException($"Apólice {evento.ApoliceId} do evento ApoliceEndossada não encontrada.");
        var endosso = apolice.Endossos.FirstOrDefault(e => e.Numero == evento.EndossoNumero);

        var modelo = ModelosNotificacao.Endosso(evento, endosso?.CoberturaNome ?? evento.CoberturaCodigo, endosso?.DataEfeito);
        notificacoes.Adicionar(FabricaNotificacao.Criar(apolice.Id, TipoNotificacao.Endosso, evento.SeguradoEmail, modelo, tempo.GetUtcNow()));
    }
}
