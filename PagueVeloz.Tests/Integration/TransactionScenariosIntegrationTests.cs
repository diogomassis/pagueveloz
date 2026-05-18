using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Application.Services;
using PagueVeloz.Infrastructure.Persistence;
using PagueVeloz.Infrastructure;

namespace PagueVeloz.Tests.Integration;

public sealed class TransactionScenariosIntegrationTests
{
    private static string? GetConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        return "Host=localhost;Port=5432;Database=pagueveloz;Username=pagueveloz;Password=pagueveloz";
    }

    private static PagueVelozDbContext? CreateContext(string conn)
    {
        var options = new DbContextOptionsBuilder<PagueVelozDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new PagueVelozDbContext(options);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Transaction_scenarios_end_to_end()
    {
        var conn = GetConnectionString();
        if (string.IsNullOrWhiteSpace(conn)) return; // skip when no integration DB
        await using var ctx = CreateContext(conn) ?? throw new InvalidOperationException("Could not create context");
        await ctx.Database.EnsureCreatedAsync();
        var repo = new EfAccountRepository(ctx);
        var uow = new EfUnitOfWork(ctx);
        var idemp = new EfIdempotencyStore(ctx);
        var lockProvider = new InMemoryAccountLockProvider();
        var eventPublisher = new InMemoryEventPublisher();
        var accountService = new AccountService(repo, uow);
        var processor = new TransactionProcessor(repo, idemp, lockProvider, eventPublisher, uow);
        // Create accounts
        await accountService.CreateAsync(new CreateAccountRequest("IT", "A1", 0, 50000));
        await accountService.CreateAsync(new CreateAccountRequest("IT", "A2", 0, 50000));
        // Credit A1
        var credit = await processor.ProcessAsync(new ProcessTransactionRequest("Credit", "A1", 100000, "BRL", "r1"));
        Assert.Equal("success", credit.Status);
        // Debit A1
        var debit = await processor.ProcessAsync(new ProcessTransactionRequest("Debit", "A1", 20000, "BRL", "r2"));
        Assert.Equal("success", debit.Status);
        // Reserve and Capture
        var reserve = await processor.ProcessAsync(new ProcessTransactionRequest("Reserve", "A1", 30000, "BRL", "r3"));
        Assert.Equal("success", reserve.Status);
        var capture = await processor.ProcessAsync(new ProcessTransactionRequest("Capture", "A1", 30000, "BRL", "r4"));
        Assert.Equal("success", capture.Status);
        // Transfer A1 -> A2
        var transfer = await processor.ProcessAsync(new ProcessTransactionRequest("Transfer", "A1", 25000, "BRL", "r5", "A2"));
        Assert.Equal("success", transfer.Status);
        // Validate balances persisted
        var a1 = await repo.GetByIdAsync("A1");
        var a2 = await repo.GetByIdAsync("A2");
        Assert.NotNull(a1);
        Assert.NotNull(a2);
        // Basic sanity: combined balance equals expected values
        var expectedA1 = 100000 - 20000 - 30000 /*captured*/ - 25000; // credit - debit - capture - transfer
        Assert.Equal(expectedA1, a1!.Balance);
        Assert.Equal(25000, a2!.Balance);
    }
}
