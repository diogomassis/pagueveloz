using PagueVeloz.Application.Events;

namespace PagueVeloz.Application.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken = default);
}