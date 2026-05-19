using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Domain;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, AccountDomain> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<InMemoryAccountRepository> _logger;

    public InMemoryAccountRepository(ILogger<InMemoryAccountRepository>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryAccountRepository>.Instance;
    }

    public Task<AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetByIdAsync {AccountId}", accountId);
        if (_accounts.TryGetValue(accountId, out var account))
        {
            _logger.LogDebug("Account found {AccountId}", accountId);
            return Task.FromResult<AccountDomain?>(account.Clone());
        }
        _logger.LogDebug("Account not found {AccountId}", accountId);
        return Task.FromResult<AccountDomain?>(null);
    }

    public Task SaveAsync(AccountDomain account, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SaveAsync {AccountId} Balance={Balance} Reserved={Reserved}", account.AccountId, account.Balance, account.ReservedBalance);
        _accounts[account.AccountId] = account.Clone();
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var exists = _accounts.ContainsKey(accountId);
        _logger.LogDebug("ExistsAsync {AccountId} => {Exists}", accountId, exists);
        return Task.FromResult(exists);
    }
}