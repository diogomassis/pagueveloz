using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    
    // Telemetry
    private static readonly Meter Meter = new("PagueVeloz.Application.AccountService");
    private static readonly Counter<long> AccountCounter = Meter.CreateCounter<long>(
        "pagueveloz.accounts.created", 
        "count", 
        "Total number of accounts created");
    private static readonly Histogram<double> AccountCreationLatency = Meter.CreateHistogram<double>(
        "pagueveloz.accounts.creation_latency_ms", 
        "ms", 
        "Account creation latency");

    public AccountService(IAccountRepository accountRepository, IUnitOfWork unitOfWork, ILogger<AccountService>? logger = null)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<AccountService>.Instance;
    }

    public async Task<CreateAccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("CreateAccount invoked for ClientId={ClientId} AccountId={AccountId}", request.ClientId, request.AccountId);
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.AccountId))
        {
            _logger.LogWarning("CreateAccount validation failed for ClientId={ClientId} AccountId={AccountId}", request.ClientId, request.AccountId);
            stopwatch.Stop();
            AccountCreationLatency.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("status", "validation_failure"));
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
        
        stopwatch.Stop();
        if (response is null)
        {
            _logger.LogError("CreateAccount failed for {AccountId}", request.AccountId);
            AccountCreationLatency.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("status", "internal_failure"));
            return CreateFailure(request.AccountId, "Account could not be created.");
        }
        
        var status = response.ErrorMessage is null ? "success" : "failure";
        AccountCounter.Add(1, new KeyValuePair<string, object?>("status", status));
        AccountCreationLatency.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("status", status));
        return response;
    }

    private static CreateAccountResponse CreateSuccess(AccountDomain account) => new(
        account.AccountId, "success", account.Balance, account.ReservedBalance,
        account.AvailableBalance, DateTimeOffset.UtcNow, null);

    private static CreateAccountResponse CreateFailure(string? accountId, string errorMessage) => new(
        accountId ?? string.Empty, "failed", 0, 0, 0,
        DateTimeOffset.UtcNow, errorMessage);
}
