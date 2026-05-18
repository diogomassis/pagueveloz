using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Domain;

namespace PagueVeloz.Application.Services;

public sealed class AccountService(IAccountRepository accountRepository, IUnitOfWork unitOfWork) : IAccountService
{
    public async Task<CreateAccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.AccountId))
            return CreateFailure(request.AccountId, "Client id and account id are required.");
        CreateAccountResponse? response = null;
        return response ?? CreateFailure(request.AccountId, "Account could not be created.");
    }

    private static CreateAccountResponse CreateSuccess(AccountDomain account) => new(
        account.AccountId, "success", account.Balance, account.ReservedBalance,
        account.AvailableBalance, DateTimeOffset.UtcNow, null);

    private static CreateAccountResponse CreateFailure(string? accountId, string errorMessage) => new(
        accountId ?? string.Empty, "failed", 0, 0, 0,
        DateTimeOffset.UtcNow, errorMessage);
}
