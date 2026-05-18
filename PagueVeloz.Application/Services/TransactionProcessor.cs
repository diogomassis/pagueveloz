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

    private static ProcessTransactionResponse Success(string referenceId, AccountDomain account, DateTimeOffset timestamp) => new(
        $"{referenceId}-PROCESSED", "success", account.Balance,
        account.ReservedBalance, account.AvailableBalance, timestamp, null);

    private static ProcessTransactionResponse Failed(string referenceId, string errorMessage) => new(
        string.IsNullOrWhiteSpace(referenceId) ? string.Empty : $"{referenceId}-PROCESSED",
        "failed", 0, 0, 0, DateTimeOffset.UtcNow, errorMessage);
}
