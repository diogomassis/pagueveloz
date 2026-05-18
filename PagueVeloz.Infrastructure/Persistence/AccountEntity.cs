namespace PagueVeloz.Infrastructure.Persistence;

public sealed class AccountEntity
{
    public string AccountId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public long Balance { get; set; }
    public long ReservedBalance { get; set; }
    public long CreditLimit { get; set; }
    public string Status { get; set; } = string.Empty;
}
