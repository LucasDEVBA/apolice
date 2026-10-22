using Apolice.Domain.Comum;

namespace Apolice.Application.Abstractions.Messaging;

public interface IQueryHandler<in TQuery, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
