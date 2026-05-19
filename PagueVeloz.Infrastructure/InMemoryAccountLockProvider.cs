using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Abstractions;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryAccountLockProvider : IAccountLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<InMemoryAccountLockProvider> _logger;

    public InMemoryAccountLockProvider(ILogger<InMemoryAccountLockProvider>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryAccountLockProvider>.Instance;
    }

    public async ValueTask<IAsyncDisposable> AcquireAsync(IEnumerable<string> accountIds, CancellationToken cancellationToken = default)
    {
        var semaphores = accountIds.Where(accountId => !string.IsNullOrWhiteSpace(accountId))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(accountId => accountId, StringComparer.OrdinalIgnoreCase)
            .Select(accountId => _locks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1))).ToArray();
        var acquired = new List<SemaphoreSlim>(semaphores.Length);
        try
        {
            _logger.LogDebug("Acquiring locks for accounts {Accounts}", string.Join(',', semaphores.Select((s, i) => i.ToString())));
            foreach (var semaphore in semaphores)
            {
                await semaphore.WaitAsync(cancellationToken);
                acquired.Add(semaphore);
            }
            _logger.LogDebug("Acquired {Count} locks", acquired.Count);
            return new LockHandle(acquired, _logger);
        }
        catch
        {
            for (var index = acquired.Count - 1; index >= 0; index--)
                acquired[index].Release();
            _logger.LogWarning("Failed to acquire all locks; released {Count} acquired locks", acquired.Count);
            throw;
        }
    }

    private sealed class LockHandle(IEnumerable<SemaphoreSlim> semaphores, ILogger logger) : IAsyncDisposable
    {
        private readonly IEnumerable<SemaphoreSlim> _semaphores = semaphores;
        private readonly ILogger _logger = logger;

        public ValueTask DisposeAsync()
        {
            foreach (var semaphore in _semaphores.Reverse())
                semaphore.Release();
            _logger.LogDebug("Released locks");
            return ValueTask.CompletedTask;
        }
    }
}
