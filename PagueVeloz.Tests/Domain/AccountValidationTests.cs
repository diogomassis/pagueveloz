using PagueVeloz.Domain;

namespace PagueVeloz.Tests.Domain;

public sealed class AccountValidationTests
{
    [Fact]
    public void Credit_increases_balance_and_requires_positive_amount()
    {
        var account = new AccountDomain("CLI-01", "ACC-01", 0);
        account.Credit(500);
        Assert.Equal(500, account.Balance);
        Assert.Throws<ExceptionDomain>(() => account.Credit(0));
        Assert.Throws<ExceptionDomain>(() => account.Credit(-1));
    }

    [Fact]
    public void Capture_reduces_reserved_and_balance_or_throws_on_insufficient_reserved()
    {
        var account = new AccountDomain("CLI-01", "ACC-01", 1000);
        account.Reserve(200);
        account.Capture(200);
        Assert.Equal(800, account.Balance);
        Assert.Equal(0, account.ReservedBalance);
        Assert.Throws<ExceptionDomain>(() => account.Capture(1));
    }

    [Fact]
    public void ReleaseReservation_decreases_reserved_or_throws()
    {
        var account = new AccountDomain("CLI-01", "ACC-01", 1000);
        account.Reserve(100);
        account.ReleaseReservation(100);
        Assert.Equal(0, account.ReservedBalance);
        Assert.Throws<ExceptionDomain>(() => account.ReleaseReservation(1));
    }

    [Fact]
    public void Constructor_validates_inputs()
    {
        Assert.Throws<ExceptionDomain>(() => new AccountDomain("", "A", 0));
        Assert.Throws<ExceptionDomain>(() => new AccountDomain("C", "", 0));
        Assert.Throws<ExceptionDomain>(() => new AccountDomain("C", "A", -1));
        Assert.Throws<ExceptionDomain>(() => new AccountDomain("C", "A", 0, -5));
    }
}
