namespace PagueVeloz.Domain;

public sealed class AccountDomain
{
    public AccountDomain(
        string clientId, string accountId, long initialBalance = 0,
        long creditLimit = 0, EnumAccountStatus status = EnumAccountStatus.Active
        )
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ExceptionDomain("Client id is required.");
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ExceptionDomain("Account id is required.");
        if (initialBalance < 0)
            throw new ExceptionDomain("Initial balance cannot be negative.");
        if (creditLimit < 0)
            throw new ExceptionDomain("Credit limit cannot be negative.");
        ClientId = clientId;
        AccountId = accountId;
        Balance = initialBalance;
        CreditLimit = creditLimit;
        Status = status;
    }

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

    public void Credit(long amount)
    {
        EnsureActive();
        EnsurePositiveAmount(amount);
        Balance += amount;
    }

    public void Debit(long amount)
    {
        EnsureActive();
        EnsurePositiveAmount(amount);

        if (AvailableBalance + CreditLimit < amount)
        {
            throw new ExceptionDomain("Insufficient balance.");
        }

        Balance -= amount;
    }

    public void Reserve(long amount)
    {
        EnsureActive();
        EnsurePositiveAmount(amount);

        if (AvailableBalance < amount)
        {
            throw new ExceptionDomain("Insufficient available balance for reservation.");
        }

        ReservedBalance += amount;
    }

    public void Capture(long amount)
    {
        EnsureActive();
        EnsurePositiveAmount(amount);

        if (ReservedBalance < amount)
        {
            throw new ExceptionDomain("Insufficient reserved balance for capture.");
        }

        ReservedBalance -= amount;
        Balance -= amount;
    }

    public void ReleaseReservation(long amount)
    {
        EnsureActive();
        EnsurePositiveAmount(amount);

        if (ReservedBalance < amount)
        {
            throw new ExceptionDomain("Insufficient reserved balance for reversal.");
        }

        ReservedBalance -= amount;
    }

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
