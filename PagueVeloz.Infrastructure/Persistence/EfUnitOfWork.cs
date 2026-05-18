using PagueVeloz.Application.Abstractions;

namespace PagueVeloz.Infrastructure.Persistence;

public sealed class EfUnitOfWork(PagueVelozDbContext dbContext) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await action(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
