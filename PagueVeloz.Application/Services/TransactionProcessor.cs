using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Events;
using PagueVeloz.Domain;

namespace PagueVeloz.Application.Services;

public sealed class TransactionProcessor : ITransactionProcessor
{
    private readonly IAccountRepository _accountRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IAccountLockProvider _accountLockProvider;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionProcessor> _logger;

    // Telemetry
    private static readonly Meter Meter = new("PagueVeloz.Application.TransactionProcessor");
    private static readonly Counter<long> TransactionCounter = Meter.CreateCounter<long>(
        "pagueveloz.transactions.processed",
        "count",
        "Total number of transactions processed");
    private static readonly Histogram<double> TransactionLatency = Meter.CreateHistogram<double>(
        "pagueveloz.transactions.latency_ms",
        "ms",
        "Transaction processing latency");

    public TransactionProcessor(IAccountRepository accountRepository, IIdempotencyStore idempotencyStore,
        IAccountLockProvider accountLockProvider, IEventPublisher eventPublisher, IUnitOfWork unitOfWork, ILogger<TransactionProcessor>? logger = null)
    {
        _accountRepository = accountRepository;
        _idempotencyStore = idempotencyStore;
        _accountLockProvider = accountLockProvider;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<TransactionProcessor>.Instance;
    }

    public async Task<ProcessTransactionResponse> ProcessAsync(ProcessTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Processing transaction {Operation} for AccountId={AccountId} ReferenceId={ReferenceId}", request.Operation, request.AccountId, request.ReferenceId);
        var validationError = Validate(request);
        if (validationError is not null)
        {
            _logger.LogWarning("Transaction validation failed {ReferenceId} {Error}", request.ReferenceId, validationError);
            return Failed(request.ReferenceId, validationError);
        }
        ProcessTransactionResponse? response = null;
        await _unitOfWork.ExecuteAsync(async unitCancellationToken =>
        {
            var cached = await _idempotencyStore.GetAsync(request.ReferenceId, unitCancellationToken);
            if (cached is not null)
            {
                _logger.LogInformation("Idempotency hit for {ReferenceId}", request.ReferenceId);
                response = cached;
                return;
            }
            _logger.LogDebug("Acquiring account locks for ReferenceId={ReferenceId}", request.ReferenceId);
            await using var lockHandle = await _accountLockProvider.AcquireAsync(GetLockKeys(request), unitCancellationToken);
            _logger.LogDebug("Account locks acquired for ReferenceId={ReferenceId}", request.ReferenceId);
            var sourceAccount = await _accountRepository.GetByIdAsync(request.AccountId, unitCancellationToken);
            if (sourceAccount is null)
            {
                _logger.LogWarning("Source account not found {AccountId} for {ReferenceId}", request.AccountId, request.ReferenceId);
                response = Failed(request.ReferenceId, "Source account not found.");
                return;
            }
            AccountDomain? targetAccount = null;
            if (IsTransfer(request.Operation))
            {
                if (string.IsNullOrWhiteSpace(request.TargetAccountId))
                {
                    _logger.LogWarning("Transfer requested without target account {ReferenceId}", request.ReferenceId);
                    response = Failed(request.ReferenceId, "Target account id is required for transfers.");
                    return;
                }
                targetAccount = await _accountRepository.GetByIdAsync(request.TargetAccountId, unitCancellationToken);
                if (targetAccount is null)
                {
                    _logger.LogWarning("Target account not found {TargetAccountId} for {ReferenceId}", request.TargetAccountId, request.ReferenceId);
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
                _logger.LogInformation("Transaction applied in-memory {ReferenceId} {TransactionId}", request.ReferenceId, response.TransactionId);
                await _eventPublisher.PublishAsync(
                    new TransactionProcessedEvent(
                        response.TransactionId,
                        request.Operation,
                        request.AccountId,
                        request.TargetAccountId,
                        request.Amount,
                        response.Status,
                        timestamp),
                    unitCancellationToken);
                _logger.LogDebug("Published TransactionProcessedEvent {TransactionId}", response.TransactionId);
                await _accountRepository.SaveAsync(workingSource, unitCancellationToken);
                if (workingTarget is not null)
                    await _accountRepository.SaveAsync(workingTarget, unitCancellationToken);
                await _idempotencyStore.SaveAsync(request.ReferenceId, response, unitCancellationToken);
                _logger.LogInformation("Transaction persisted and idempotency saved {ReferenceId}", request.ReferenceId);
            }
            catch (ExceptionDomain exception)
            {
                _logger.LogWarning("Domain exception while processing {ReferenceId}: {Error}", request.ReferenceId, exception.Message);
                response = Failed(request.ReferenceId, exception.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unhandled exception while processing {ReferenceId}", request.ReferenceId);
                response = Failed(request.ReferenceId, exception.Message);
            }
        }, cancellationToken);

        stopwatch.Stop();
        var operation = request.Operation ?? "unknown";
        var status = response?.ErrorMessage is null ? "success" : "failure";
        TransactionCounter.Add(1, new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("status", status));
        TransactionLatency.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("status", status));

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
