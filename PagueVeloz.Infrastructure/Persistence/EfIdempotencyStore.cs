using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Infrastructure.Persistence;

public sealed class EfIdempotencyStore(PagueVelozDbContext dbContext) : IIdempotencyStore
{
    public async Task<ProcessTransactionResponse?> GetAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        return null;
    }

    public async Task SaveAsync(string referenceId, ProcessTransactionResponse response, CancellationToken cancellationToken = default)
    {
        return;
    }
}