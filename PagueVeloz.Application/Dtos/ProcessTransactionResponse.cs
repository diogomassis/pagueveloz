namespace PagueVeloz.Application.Dtos;

public sealed record ProcessTransactionResponse(
    string TransactionId,
    string Status,
    long Balance,
    long ReservedBalance,
    long AvailableBalance,
    DateTimeOffset Timestamp,
    string? ErrorMessage);