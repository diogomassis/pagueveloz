namespace PagueVeloz.Infrastructure.Persistence;

public sealed class IdempotencyRecordEntity
{
    public string ReferenceId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long Balance { get; set; }
    public long ReservedBalance { get; set; }
    public long AvailableBalance { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}