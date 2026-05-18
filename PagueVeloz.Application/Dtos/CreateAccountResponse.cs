namespace PagueVeloz.Application.Dtos;

public sealed record CreateAccountResponse(
    string AccountId,
    string Status,
    long Balance,
    long ReservedBalance,
    long AvailableBalance,
    DateTimeOffset Timestamp,
    string? ErrorMessage);