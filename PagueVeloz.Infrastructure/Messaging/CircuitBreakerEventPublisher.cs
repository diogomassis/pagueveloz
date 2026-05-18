using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Events;

namespace PagueVeloz.Infrastructure.Messaging;

public sealed class CircuitBreakerEventPublisher : IEventPublisher
{
    private readonly IEventPublisher _inner;
    private readonly IDistributedCache? _cache;
    private readonly ILogger<CircuitBreakerEventPublisher> _logger;
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly int _halfOpenMaxTrials;
    private readonly object _sync = new();
    private int _failureCount;
    private DateTime _openUntilUtc = DateTime.MinValue;
    private int _halfOpenTrialCount;
    private CircuitState _state = CircuitState.Closed;
    private enum CircuitState
    {
        Closed, Open, HalfOpen
    }

    public CircuitBreakerEventPublisher(IEventPublisher inner, IDistributedCache? cache, ILogger<CircuitBreakerEventPublisher> logger, IConfiguration configuration)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _failureThreshold = int.TryParse(configuration["Messaging:RabbitMq:CircuitBreaker:FailureThreshold"], out var ft) && ft > 0 ? ft :
                            (int.TryParse(Environment.GetEnvironmentVariable("PAGUEVELOZ_CB_FAILURE_THRESHOLD"), out var eft) && eft > 0 ? eft : 5);
        _openDuration = int.TryParse(configuration["Messaging:RabbitMq:CircuitBreaker:OpenDurationMs"], out var od) && od > 0 ? TimeSpan.FromMilliseconds(od) :
                        (int.TryParse(Environment.GetEnvironmentVariable("PAGUEVELOZ_CB_OPEN_DURATION_MS"), out var eod) && eod > 0 ? TimeSpan.FromMilliseconds(eod) : TimeSpan.FromSeconds(30));
        _halfOpenMaxTrials = int.TryParse(configuration["Messaging:RabbitMq:CircuitBreaker:HalfOpenMaxTrials"], out var ht) && ht > 0 ? ht :
                             (int.TryParse(Environment.GetEnvironmentVariable("PAGUEVELOZ_CB_HALF_OPEN_TRIALS"), out var eht) && eht > 0 ? eht : 3);
    }

    public async Task PublishAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken = default)
    {
        // fast-check state under lock
        lock (_sync)
        {
            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow < _openUntilUtc)
                {
                    _logger.LogWarning("Circuit open - short-circuiting publish and persisting to fallback.");
                    _ = PersistFallbackAsync(@event, cancellationToken);
                    return;
                }
                // move to half-open
                _state = CircuitState.HalfOpen;
                _halfOpenTrialCount = 0;
                _logger.LogInformation("Circuit transitioning to HalfOpen for trial publishes.");
            }
        }
        try
        {
            await _inner.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                // success => reset
                _failureCount = 0;
                _state = CircuitState.Closed;
                _halfOpenTrialCount = 0;
            }
            _logger.LogDebug("Publish succeeded and circuit is Closed.");
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            var shouldOpen = false;
            lock (_sync)
            {
                _failureCount++;
                if (_state == CircuitState.HalfOpen)
                {
                    _halfOpenTrialCount++;
                    if (_halfOpenTrialCount >= _halfOpenMaxTrials)
                        shouldOpen = true;
                }
                else if (_failureCount >= _failureThreshold)
                {
                    shouldOpen = true;
                }
                if (shouldOpen)
                {
                    _state = CircuitState.Open;
                    _openUntilUtc = DateTime.UtcNow.Add(_openDuration);
                    _logger.LogWarning(ex, "Circuit opened after {Failures} failures. Open until {OpenUntil}.", _failureCount, _openUntilUtc);
                }
                else
                {
                    _logger.LogWarning(ex, "Publish failed (failureCount={Failures}).", _failureCount);
                }
            }
            // persist to fallback store for durability
            await PersistFallbackAsync(@event, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PersistFallbackAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken)
    {
        if (_cache == null)
        {
            _logger.LogError("No fallback cache available; dropping event.");
            return;
        }
        try
        {
            var key = $"pagueveloz:outbox:{Guid.NewGuid():N}";
            var payload = JsonSerializer.SerializeToUtf8Bytes(@event);
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) };
            await _cache.SetAsync(key, payload, options, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Persisted event to fallback cache key={Key}.", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist event to fallback cache.");
        }
    }
}
