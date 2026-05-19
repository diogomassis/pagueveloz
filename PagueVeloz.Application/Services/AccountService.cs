using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Domain;

namespace PagueVeloz.Application.Services;

public sealed class AccountService : IAccountService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AccountService> _logger;

    public AccountService(IAccountRepository accountRepository, IUnitOfWork unitOfWork, ILogger<AccountService>? logger = null)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<AccountService>.Instance;
    }

    public async Task<CreateAccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("CreateAccount invoked for ClientId={ClientId} AccountId={AccountId}", request.ClientId, request.AccountId);
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.AccountId))
        {
            _logger.LogWarning("CreateAccount validation failed for ClientId={ClientId} AccountId={AccountId}", request.ClientId, request.AccountId);
            return CreateFailure(request.AccountId, "Client id and account id are required.");
        }
        CreateAccountResponse? response = null;
        await _unitOfWork.ExecuteAsync(async unitCancellationToken =>
        {
            if (await _accountRepository.ExistsAsync(request.AccountId, unitCancellationToken))
            {
                _logger.LogWarning("CreateAccount rejected; account already exists {AccountId}", request.AccountId);
                response = CreateFailure(request.AccountId, "Account already exists.");
                return;
            }
            var account = new AccountDomain(request.ClientId, request.AccountId, request.InitialBalance, request.CreditLimit);
            await _accountRepository.SaveAsync(account, unitCancellationToken);
            response = CreateSuccess(account);
            _logger.LogInformation("Account created {AccountId}", account.AccountId);
        }, cancellationToken);
        if (response is null)
        {
            _logger.LogError("CreateAccount failed for {AccountId}", request.AccountId);
            return CreateFailure(request.AccountId, "Account could not be created.");
        }
        return response;
    }

    private static CreateAccountResponse CreateSuccess(AccountDomain account) => new(
        account.AccountId, "success", account.Balance, account.ReservedBalance,
        account.AvailableBalance, DateTimeOffset.UtcNow, null);

    private static CreateAccountResponse CreateFailure(string? accountId, string errorMessage) => new(
        accountId ?? string.Empty, "failed", 0, 0, 0,
        DateTimeOffset.UtcNow, errorMessage);
}
