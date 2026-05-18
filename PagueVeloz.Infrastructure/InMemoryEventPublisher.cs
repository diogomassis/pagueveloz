using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Events;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryEventPublisher : IEventPublisher
{
    public Task PublishAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
