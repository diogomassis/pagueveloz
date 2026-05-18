using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Application.Dtos;
using PagueVeloz.Infrastructure.Caching;
using Xunit;

namespace PagueVeloz.Tests.Integration;

public class RedisIdempotencyIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RedisIdempotencyStore_roundtrip_integration()
    {
        var conn = Environment.GetEnvironmentVariable("TEST_REDIS_CONNECTION") ?? "localhost:6379";
        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(opts => { opts.Configuration = conn; });
        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();
        var store = new RedisIdempotencyStore(cache);
        var referenceId = Guid.NewGuid().ToString("N");
        var response = new ProcessTransactionResponse(
            TransactionId: Guid.NewGuid().ToString(),
            Status: "success",
            Balance: 1000,
            ReservedBalance: 0,
            AvailableBalance: 1000,
            Timestamp: DateTimeOffset.UtcNow,
            ErrorMessage: null
        );
        await store.SaveAsync(referenceId, response);
        var got = await store.GetAsync(referenceId);
        Assert.NotNull(got);
        Assert.Equal(response.TransactionId, got!.TransactionId);
        Assert.Equal(response.Status, got.Status);
        Assert.Equal(response.Balance, got.Balance);
        Assert.Equal(response.AvailableBalance, got.AvailableBalance);
    }
}
