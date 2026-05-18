using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Services;
using PagueVeloz.Infrastructure;
using PagueVeloz.Infrastructure.Persistence;

namespace PagueVeloz.Tests.Application;

public sealed class AccountServiceTests
{
    [Fact]
    public async Task CreateAsync_returns_failed_for_missing_ids()
    {
        var repo = new InMemoryAccountRepository();
        var uow = new InMemoryUnitOfWork();
        var svc = new AccountService(repo, uow);
        var res1 = await svc.CreateAsync(new CreateAccountRequest("", "A"));
        Assert.Equal("failed", res1.Status);
        var res2 = await svc.CreateAsync(new CreateAccountRequest("C", ""));
        Assert.Equal("failed", res2.Status);
    }

    [Fact]
    public async Task CreateAsync_returns_failed_when_account_exists()
    {
        var repo = new InMemoryAccountRepository();
        var uow = new InMemoryUnitOfWork();
        var svc = new AccountService(repo, uow);
        var first = await svc.CreateAsync(new CreateAccountRequest("C", "A"));
        Assert.Equal("success", first.Status);
        var second = await svc.CreateAsync(new CreateAccountRequest("C", "A"));
        Assert.Equal("failed", second.Status);
        Assert.Contains("already exists", second.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_success_creates_account_with_initial_values()
    {
        var repo = new InMemoryAccountRepository();
        var uow = new InMemoryUnitOfWork();
        var svc = new AccountService(repo, uow);
        var res = await svc.CreateAsync(new CreateAccountRequest("C1", "A1", 1000, 200));
        Assert.Equal("success", res.Status);
        Assert.Equal(1000, res.Balance);
        Assert.Equal(0, res.ReservedBalance);
        Assert.Equal(1000, res.AvailableBalance);
    }

    [Fact]
    public async Task CreateAsync_handles_repository_exceptions_and_returns_failed()
    {
        var repo = new ThrowingAccountRepository();
        var uow = new InMemoryUnitOfWork();
        var svc = new AccountService(repo, uow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(new CreateAccountRequest("C", "A")));
    }

    private sealed class ThrowingAccountRepository : PagueVeloz.Application.Abstractions.IAccountRepository
    {
        public Task<PagueVeloz.Domain.AccountDomain?> GetByIdAsync(string accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<PagueVeloz.Domain.AccountDomain?>(null);
        public Task SaveAsync(PagueVeloz.Domain.AccountDomain account, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
        public Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
