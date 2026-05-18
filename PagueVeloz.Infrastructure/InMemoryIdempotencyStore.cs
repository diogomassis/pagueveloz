using System.Collections.Concurrent;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, ProcessTransactionResponse> _responses = new(StringComparer.OrdinalIgnoreCase);

    public Task<ProcessTransactionResponse?> GetAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_responses.TryGetValue(referenceId, out var response) ? response : null);
    }

    public Task SaveAsync(string referenceId, ProcessTransactionResponse response, CancellationToken cancellationToken = default)
    {
        _responses[referenceId] = response;
        return Task.CompletedTask;
    }
}
