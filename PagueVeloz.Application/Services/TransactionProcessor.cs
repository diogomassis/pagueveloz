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
        var validationError = Validate(request);
        if (validationError is not null)
            return Failed(request.ReferenceId, validationError);
        ProcessTransactionResponse? response = null;
        await unitOfWork.ExecuteAsync(async unitCancellationToken =>
        {
            var cached = await idempotencyStore.GetAsync(request.ReferenceId, unitCancellationToken);
            if (cached is not null)
            {
                response = cached;
                return;
            }
            await using var lockHandle = await accountLockProvider.AcquireAsync(GetLockKeys(request), unitCancellationToken);
            var sourceAccount = await accountRepository.GetByIdAsync(request.AccountId, unitCancellationToken);
            if (sourceAccount is null)
            {
                response = Failed(request.ReferenceId, "Source account not found.");
                return;
            }
            AccountDomain? targetAccount = null;
            if (IsTransfer(request.Operation))
            {
                if (string.IsNullOrWhiteSpace(request.TargetAccountId))
                {
                    response = Failed(request.ReferenceId, "Target account id is required for transfers.");
                    return;
                }
                targetAccount = await accountRepository.GetByIdAsync(request.TargetAccountId, unitCancellationToken);
                if (targetAccount is null)
                {
                    response = Failed(request.ReferenceId, "Target account not found.");
                    return;
                }
            }
            var workingSource = sourceAccount.Clone();
            var workingTarget = targetAccount?.Clone();
            try
            {
                ApplyOperation(request, workingSource, workingTarget);
                var timestamp = DateTimeOffset.UtcNow;
                response = Success(request.ReferenceId, workingSource, timestamp);
                await eventPublisher.PublishAsync(
                    new TransactionProcessedEvent(
                        response.TransactionId,
                        request.Operation,
                        request.AccountId,
                        request.TargetAccountId,
                        request.Amount,
                        response.Status,
                        timestamp),
                    unitCancellationToken);
                await accountRepository.SaveAsync(workingSource, unitCancellationToken);
                if (workingTarget is not null)
                    await accountRepository.SaveAsync(workingTarget, unitCancellationToken);
                await idempotencyStore.SaveAsync(request.ReferenceId, response, unitCancellationToken);
            }
            catch (ExceptionDomain exception)
            {
                response = Failed(request.ReferenceId, exception.Message);
            }
            catch (Exception exception)
            {
                response = Failed(request.ReferenceId, exception.Message);
            }
        }, cancellationToken);
        return response ?? Failed(request.ReferenceId, "Transaction could not be processed.");
    }

    private static void ApplyOperation(ProcessTransactionRequest request, AccountDomain sourceAccount, AccountDomain? targetAccount)
    {
        var operation = ParseOperation(request.Operation);
        switch (operation)
        {
            case EnumOperationType.Credit:
                sourceAccount.Credit(request.Amount);
                return;
            case EnumOperationType.Debit:
                sourceAccount.Debit(request.Amount);
                return;
            case EnumOperationType.Reserve:
                sourceAccount.Reserve(request.Amount);
                return;
            case EnumOperationType.Capture:
                sourceAccount.Capture(request.Amount);
                return;
            case EnumOperationType.Reversal:
                ApplyReversal(request, sourceAccount);
                return;
            case EnumOperationType.Transfer:
                if (targetAccount is null)
                    throw new ExceptionDomain("Target account is required for transfers.");
                sourceAccount.Debit(request.Amount);
                targetAccount.Credit(request.Amount);
                return;
            default:
                throw new ExceptionDomain($"Operation '{request.Operation}' is not supported.");
        }
    }

    private static void ApplyReversal(ProcessTransactionRequest request, AccountDomain account)
    {
        var originalOperation = GetMetadataValue(request.Metadata, "original_operation");
        if (string.IsNullOrWhiteSpace(originalOperation))
            throw new ExceptionDomain("Reversal requires metadata.original_operation.");
        switch (originalOperation.Trim().ToLowerInvariant())
        {
            case "credit":
                account.Debit(request.Amount);
                return;
            case "debit":
                account.Credit(request.Amount);
                return;
            case "reserve":
                account.ReleaseReservation(request.Amount);
                return;
            case "capture":
                account.Credit(request.Amount);
                account.Reserve(request.Amount);
                return;
            default:
                throw new ExceptionDomain($"Reversal of '{originalOperation}' is not supported yet.");
        }
    }

    private static string? GetMetadataValue(JsonElement? metadata, string propertyName)
    {
        if (metadata is not { ValueKind: JsonValueKind.Object })
            return null;
        if (!metadata.Value.TryGetProperty(propertyName, out var property))
            return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static IEnumerable<string> GetLockKeys(ProcessTransactionRequest request)
    {
        return IsTransfer(request.Operation) && !string.IsNullOrWhiteSpace(request.TargetAccountId)
            ? [request.AccountId, request.TargetAccountId]
            : [request.AccountId];
    }

    private static bool IsTransfer(string operation)
    {
        return string.Equals(operation, nameof(EnumOperationType.Transfer), StringComparison.OrdinalIgnoreCase);
    }

    private static EnumOperationType ParseOperation(string operation)
    {
        if (!Enum.TryParse<EnumOperationType>(operation, true, out var parsedOperation))
        {
            throw new ExceptionDomain($"Unsupported operation '{operation}'.");
        }

        return parsedOperation;
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
