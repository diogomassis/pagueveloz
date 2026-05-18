using PagueVeloz.Application.Abstractions;
using PagueVeloz.Domain;

namespace PagueVeloz.Infrastructure.Persistence;

public sealed class EfAccountRepository(PagueVelozDbContext dbContext) : IAccountRepository
{
    public async Task<AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return null;
    }

    public async Task SaveAsync(AccountDomain account, CancellationToken cancellationToken = default)
    {
        return;
    }

    public Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return null;
    }
}
