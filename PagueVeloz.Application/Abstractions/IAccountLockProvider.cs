namespace PagueVeloz.Application.Abstractions;

public interface IAccountLockProvider
{
    ValueTask<IAsyncDisposable> AcquireAsync(IEnumerable<string> accountIds, CancellationToken cancellationToken = default);
}