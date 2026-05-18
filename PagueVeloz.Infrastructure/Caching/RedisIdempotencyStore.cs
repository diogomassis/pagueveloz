using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Infrastructure.Caching;

public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _options = new() { SlidingExpiration = TimeSpan.FromDays(7) };
    public RedisIdempotencyStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string GetKey(string referenceId) => $"idemp:{referenceId}";

    public async Task<ProcessTransactionResponse?> GetAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(GetKey(referenceId), cancellationToken);
        if (bytes is null || bytes.Length == 0) return null;
        return JsonSerializer.Deserialize<ProcessTransactionResponse>(bytes);
    }

    public async Task SaveAsync(string referenceId, ProcessTransactionResponse response, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response);
        await _cache.SetAsync(GetKey(referenceId), bytes, _options, cancellationToken);
    }

}
