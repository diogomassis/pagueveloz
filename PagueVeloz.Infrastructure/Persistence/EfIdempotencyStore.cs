using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Infrastructure.Persistence;

public sealed class EfIdempotencyStore(PagueVelozDbContext dbContext) : IIdempotencyStore
{
    public async Task<ProcessTransactionResponse?> GetAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(record => record.ReferenceId == referenceId, cancellationToken);
        return entity is null ? null
            : new ProcessTransactionResponse(entity.TransactionId, entity.Status, entity.Balance,
            entity.ReservedBalance, entity.AvailableBalance, entity.Timestamp, entity.ErrorMessage);
    }

    public async Task SaveAsync(string referenceId, ProcessTransactionResponse response, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.IdempotencyRecords.FirstOrDefaultAsync(record => record.ReferenceId == referenceId, cancellationToken);
        if (existing is null)
        {
            dbContext.IdempotencyRecords.Add(new IdempotencyRecordEntity
            {
                ReferenceId = referenceId,
                TransactionId = response.TransactionId,
                Status = response.Status,
                Balance = response.Balance,
                ReservedBalance = response.ReservedBalance,
                AvailableBalance = response.AvailableBalance,
                Timestamp = response.Timestamp,
                ErrorMessage = response.ErrorMessage
            });
            return;
        }
        existing.TransactionId = response.TransactionId;
        existing.Status = response.Status;
        existing.Balance = response.Balance;
        existing.ReservedBalance = response.ReservedBalance;
        existing.AvailableBalance = response.AvailableBalance;
        existing.Timestamp = response.Timestamp;
        existing.ErrorMessage = response.ErrorMessage;
    }
}