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
    public AccountDomain Clone() => new(ClientId, AccountId, Balance, CreditLimit, Status)
    {
        ReservedBalance = ReservedBalance
    };

    private static void EnsurePositiveAmount(long amount)
    {
        if (amount <= 0)
        {
            throw new ExceptionDomain("Amount must be greater than zero.");
        }
    }

    private void EnsureActive()
    {
        if (Status != EnumAccountStatus.Active)
        {
            throw new ExceptionDomain("Account is not active.");
        }
    }
}
