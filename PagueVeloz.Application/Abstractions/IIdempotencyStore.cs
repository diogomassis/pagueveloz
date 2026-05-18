using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<ProcessTransactionResponse?> GetAsync(string referenceId, CancellationToken cancellationToken = default);

    Task SaveAsync(string referenceId, ProcessTransactionResponse response, CancellationToken cancellationToken = default);
}