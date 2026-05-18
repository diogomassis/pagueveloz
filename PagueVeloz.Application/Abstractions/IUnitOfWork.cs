namespace PagueVeloz.Application.Abstractions;

public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}