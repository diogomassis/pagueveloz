using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, ProcessTransactionResponse> _responses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<InMemoryIdempotencyStore> _logger;

    public InMemoryIdempotencyStore(ILogger<InMemoryIdempotencyStore>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryIdempotencyStore>.Instance;
    }

    public Task<ProcessTransactionResponse?> GetAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("IdempotencyStore.Get {ReferenceId}", referenceId);
        return Task.FromResult(_responses.TryGetValue(referenceId, out var response) ? response : null);
    }

    public Task SaveAsync(string referenceId, ProcessTransactionResponse response, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("IdempotencyStore.Save {ReferenceId} {Status}", referenceId, response.Status);
        _responses[referenceId] = response;
        return Task.CompletedTask;
    }
}
