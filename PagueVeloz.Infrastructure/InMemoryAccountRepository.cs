using System.Collections.Concurrent;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Domain;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, AccountDomain> _accounts = new(StringComparer.OrdinalIgnoreCase);

    public Task<AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (_accounts.TryGetValue(accountId, out var account))
        {
            return Task.FromResult<AccountDomain?>(account.Clone());
        }
        return Task.FromResult<AccountDomain?>(null);
    }

    public Task SaveAsync(AccountDomain account, CancellationToken cancellationToken = default)
    {
        _accounts[account.AccountId] = account.Clone();
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accounts.ContainsKey(accountId));
    }
}