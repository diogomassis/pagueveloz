using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Domain;

namespace PagueVeloz.Infrastructure.Persistence;

public sealed class EfAccountRepository(PagueVelozDbContext dbContext) : IAccountRepository
{
    public async Task<AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(account => account.AccountId == accountId, cancellationToken);
        return entity?.ToDomain();
    }

    public async Task SaveAsync(AccountDomain account, CancellationToken cancellationToken = default)
    {
        var entity = account.ToEntity();
        var existing = await dbContext.Accounts.FirstOrDefaultAsync(item => item.AccountId == account.AccountId, cancellationToken);
        if (existing is null)
        {
            dbContext.Accounts.Add(entity);
            return;
        }
        existing.ClientId = entity.ClientId;
        existing.Balance = entity.Balance;
        existing.ReservedBalance = entity.ReservedBalance;
        existing.CreditLimit = entity.CreditLimit;
        existing.Status = entity.Status;
    }

    public Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return dbContext.Accounts.AnyAsync(account => account.AccountId == accountId, cancellationToken);
    }
}
