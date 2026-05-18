using PagueVeloz.Domain;

namespace PagueVeloz.Application.Abstractions;

public interface IAccountRepository
{
    Task<AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default);

    Task SaveAsync(AccountDomain account, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default);
}