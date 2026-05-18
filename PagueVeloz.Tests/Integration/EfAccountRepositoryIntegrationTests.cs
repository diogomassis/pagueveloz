using Microsoft.EntityFrameworkCore;
using PagueVeloz.Infrastructure.Persistence;

namespace PagueVeloz.Tests.Integration;

public sealed class EfAccountRepositoryIntegrationTests
{
    private static string? GetConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        // Default to docker-compose Postgres settings for convenience
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
    public async Task EfAccountRepository_roundtrip_create_and_update()
    {
        var conn = GetConnectionString();
        if (string.IsNullOrWhiteSpace(conn)) return; // no integration DB configured
        await using var ctx = CreateContext(conn) ?? throw new InvalidOperationException("Could not create context");
        await ctx.Database.EnsureCreatedAsync();
        var repo = new EfAccountRepository(ctx);
        var uow = new EfUnitOfWork(ctx);
        var account = new PagueVeloz.Domain.AccountDomain("IT-C", "IT-A", 1000, 200);
        await uow.ExecuteAsync(_ => repo.SaveAsync(account));
        var fetched = await repo.GetByIdAsync("IT-A");
        Assert.NotNull(fetched);
        Assert.Equal(1000, fetched!.Balance);
        // update
        fetched.Credit(500);
        await uow.ExecuteAsync(_ => repo.SaveAsync(fetched));
        var updated = await repo.GetByIdAsync("IT-A");
        Assert.Equal(1500, updated!.Balance);
    }
}
