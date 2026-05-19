using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Events;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly ILogger<InMemoryEventPublisher> _logger;

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryEventPublisher>.Instance;
    }

    public Task PublishAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("PublishEvent TransactionProcessed {TransactionId} Operation={Operation} AccountId={AccountId}", @event.TransactionId, @event.Operation, @event.AccountId);
        return Task.CompletedTask;
    }
}
