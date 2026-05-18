using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Domain;

namespace PagueVeloz.Infrastructure.Caching;

public sealed class CachedAccountRepository : IAccountRepository
{
    private readonly IAccountRepository _inner;
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _options = new() { SlidingExpiration = TimeSpan.FromSeconds(60) };
    public CachedAccountRepository(IAccountRepository inner, IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    private static string GetKey(string accountId) => $"account:{accountId}";

    public async Task<AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(accountId);
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is not null && bytes.Length > 0)
        {
            try
            {
                var account = JsonSerializer.Deserialize<AccountDomain>(bytes);
                if (account is not null) return account.Clone();
            }
            catch { }
        }
        var fromDb = await _inner.GetByIdAsync(accountId, cancellationToken);
        if (fromDb is not null)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(fromDb);
            await _cache.SetAsync(key, payload, _options, cancellationToken);
        }
        return fromDb;
    }

    public async Task SaveAsync(AccountDomain account, CancellationToken cancellationToken = default)
    {
        await _inner.SaveAsync(account, cancellationToken);
        var key = GetKey(account.AccountId);
        var payload = JsonSerializer.SerializeToUtf8Bytes(account);
        await _cache.SetAsync(key, payload, _options, cancellationToken);
    }

    public Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return _inner.ExistsAsync(accountId, cancellationToken);
    }

}
