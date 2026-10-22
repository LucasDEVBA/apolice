using Apolice.Domain.Comum;

namespace Apolice.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
