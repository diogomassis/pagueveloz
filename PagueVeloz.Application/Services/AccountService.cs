using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Application.Services;

public sealed class AccountService : IAccountService
{
    public async Task<CreateAccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        return null;
    }

    private static CreateAccountResponse CreateSuccess(AccountDomain account) => new(
        account.AccountId, "success", account.Balance, account.ReservedBalance,
        account.AvailableBalance, DateTimeOffset.UtcNow, null);

    private static CreateAccountResponse CreateFailure(string? accountId, string errorMessage) => new(
        accountId ?? string.Empty, "failed", 0, 0, 0,
        DateTimeOffset.UtcNow, errorMessage);
}
