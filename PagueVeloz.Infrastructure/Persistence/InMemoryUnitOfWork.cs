using PagueVeloz.Application.Abstractions;

namespace PagueVeloz.Infrastructure.Persistence;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        return action(cancellationToken);
    }
}
