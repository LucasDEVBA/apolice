using Apolice.Infrastructure.Messaging.RabbitMq;

namespace Apolice.Worker.Hospedagem;

// O consumidor já reconecta sozinho e só termina no cancelamento; o host só lhe dá o ciclo de vida. No shutdown, a
// mensagem em processamento recebe o cancelamento e volta para a fila (nack com requeue), sem perda.
public sealed class RabbitMqConsumerService(RabbitMqConsumidor consumidor) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => consumidor.ConsumirAsync(stoppingToken);
}
