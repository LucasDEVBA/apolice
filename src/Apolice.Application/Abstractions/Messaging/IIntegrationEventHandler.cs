using Apolice.Domain.Comum;

namespace Apolice.Application.Abstractions.Messaging;

// O handler NÃO salva: quem chama SaveChanges é o dispatcher, para gravar o efeito e o registro
// do inbox na mesma transação (idempotência por mensagem e consumidor).
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent evento, CancellationToken cancellationToken);
}
