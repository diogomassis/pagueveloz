using PagueVeloz.Domain;

namespace PagueVeloz.Tests.Domain;

public sealed class AccountTests
{
    [Fact]
    public void Reserve_moves_amount_to_reserved_balance()
    {
        var account = new AccountDomain("CLI-001", "ACC-001", 1000, 0);
        account.Reserve(300);
        Assert.Equal(1000, account.Balance);
        Assert.Equal(300, account.ReservedBalance);
        Assert.Equal(700, account.AvailableBalance);
    }

    [Fact]
    public void Debit_uses_credit_limit_when_available_balance_is_not_enough()
    {
        var account = new AccountDomain("CLI-001", "ACC-001", 300, 500);
        account.Debit(600);
        Assert.Equal(-300, account.Balance);
        Assert.Equal(0, account.ReservedBalance);
        Assert.Equal(-300, account.AvailableBalance);
    }
}