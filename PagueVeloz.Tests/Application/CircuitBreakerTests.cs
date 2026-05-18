using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Events;
using PagueVeloz.Infrastructure.Messaging;
using Xunit;

namespace PagueVeloz.Tests.Application;

public class CircuitBreakerTests
{
    private sealed class TestDistributedCache : Microsoft.Extensions.Caching.Distributed.IDistributedCache
    {
        public readonly ConcurrentDictionary<string, byte[]> Store = new();
        public byte[]? Get(string key) => Store.TryGetValue(key, out var v) ? v : null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) { return Task.CompletedTask; }
        public void Remove(string key) => Store.TryRemove(key, out _);
        public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
        public void Set(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options) => Store[key] = value;
        public Task SetAsync(string key, byte[] value, Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options, CancellationToken token = default) { Store[key] = value; return Task.CompletedTask; }
    }

    private sealed class FailingPublisher : IEventPublisher
    {
        public int Calls;
        public Task PublishAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("simulated publish failure");
        }
    }

    [Fact]
    public async Task CircuitOpensAndPersistsFallback_OnRepeatedFailures()
    {
        var failing = new FailingPublisher();
        var cache = new TestDistributedCache();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new[] {
            new KeyValuePair<string,string?>("Messaging:RabbitMq:CircuitBreaker:FailureThreshold","2"),
            new KeyValuePair<string,string?>("Messaging:RabbitMq:CircuitBreaker:OpenDurationMs","2000"),
            new KeyValuePair<string,string?>("Messaging:RabbitMq:CircuitBreaker:HalfOpenMaxTrials","1"),
        }).Build();
        var logger = new NullLogger<CircuitBreakerEventPublisher>();
        var cb = new CircuitBreakerEventPublisher(failing, cache, logger, config);
        var ev = new TransactionProcessedEvent(Guid.NewGuid().ToString(), "Debit", "acct-1", null, 100L, "Processed", DateTimeOffset.UtcNow);
        await cb.PublishAsync(ev);
        await cb.PublishAsync(ev);
        var before = cache.Store.Count;
        await cb.PublishAsync(ev);
        Assert.Equal(2, failing.Calls);
        Assert.True(cache.Store.Count > before, "Fallback cache should contain persisted events.");
    }
}
