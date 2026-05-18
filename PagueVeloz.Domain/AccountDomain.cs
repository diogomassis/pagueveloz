namespace PagueVeloz.Domain;

public sealed class AccountDomain
{
    public string ClientId { get; }
    public string AccountId { get; }
    public long Balance { get; private set; }
    public long ReservedBalance { get; private set; }
    public long CreditLimit { get; private set; }
    public EnumAccountStatus Status { get; private set; }
    public long AvailableBalance => Balance - ReservedBalance;
}
