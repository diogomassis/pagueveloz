namespace PagueVeloz.Application.Events;

public sealed record TransactionProcessedEvent(
    string TransactionId,
    string Operation,
    string AccountId,
    string? TargetAccountId,
    long Amount,
    string Status,
    DateTimeOffset Timestamp);