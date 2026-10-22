namespace Apolice.Application.Abstractions.Messaging;

// Porta de entrada dos consumidores (RabbitMQ, SQS ou InMemory): recebe a mensagem crua do outbox.
// "tipo" é o nome da classe do evento de domínio e "payload" o JSON gerado com JsonOpcoes.Padrao.
public interface IIntegrationEventDispatcher
{
    Task DispatchAsync(Guid mensagemId, string tipo, string payload, CancellationToken cancellationToken);
}
