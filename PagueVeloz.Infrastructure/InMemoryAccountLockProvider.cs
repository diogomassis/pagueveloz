using System.Collections.Concurrent;
using PagueVeloz.Application.Abstractions;

namespace PagueVeloz.Infrastructure;

public sealed class InMemoryAccountLockProvider : IAccountLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(IEnumerable<string> accountIds, CancellationToken cancellationToken = default)
    {
        var semaphores = accountIds.Where(accountId => !string.IsNullOrWhiteSpace(accountId))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(accountId => accountId, StringComparer.OrdinalIgnoreCase)
            .Select(accountId => _locks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1))).ToArray();
        var acquired = new List<SemaphoreSlim>(semaphores.Length);
        try
        {
            foreach (var semaphore in semaphores)
            {
                await semaphore.WaitAsync(cancellationToken);
                acquired.Add(semaphore);
            }
            return new LockHandle(acquired);
        }
        catch
        {
            for (var index = acquired.Count - 1; index >= 0; index--)
                acquired[index].Release();
            throw;
        }
    }

    private sealed class LockHandle(IEnumerable<SemaphoreSlim> semaphores) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            foreach (var semaphore in semaphores.Reverse())
                semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
