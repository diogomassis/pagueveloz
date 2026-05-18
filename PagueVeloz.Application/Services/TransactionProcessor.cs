using System.Text.Json;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Events;
using PagueVeloz.Domain;

namespace PagueVeloz.Application.Services;

public sealed class TransactionProcessor(IAccountRepository accountRepository, IIdempotencyStore idempotencyStore,
    IAccountLockProvider accountLockProvider, IEventPublisher eventPublisher, IUnitOfWork unitOfWork) : ITransactionProcessor
{
    public async Task<ProcessTransactionResponse> ProcessAsync(ProcessTransactionRequest request, CancellationToken cancellationToken = default)
    {
        return null;
    }


    private static string? Validate(ProcessTransactionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Operation))
            return "Operation is required.";
        if (string.IsNullOrWhiteSpace(request.AccountId))
            return "Account id is required.";
        if (string.IsNullOrWhiteSpace(request.ReferenceId))
            return "Reference id is required.";
        if (request.Amount <= 0)
            return "Amount must be greater than zero.";
        if (string.IsNullOrWhiteSpace(request.Currency))
            return "Currency is required.";
        if (!string.Equals(request.Currency, "BRL", StringComparison.OrdinalIgnoreCase))
            return "Only BRL is supported in the first version.";
        return null;
    }

    private static ProcessTransactionResponse Success(string referenceId, AccountDomain account, DateTimeOffset timestamp) => new(
        $"{referenceId}-PROCESSED", "success", account.Balance,
        account.ReservedBalance, account.AvailableBalance, timestamp, null);

    private static ProcessTransactionResponse Failed(string referenceId, string errorMessage) => new(
        string.IsNullOrWhiteSpace(referenceId) ? string.Empty : $"{referenceId}-PROCESSED",
        "failed", 0, 0, 0, DateTimeOffset.UtcNow, errorMessage);
}
