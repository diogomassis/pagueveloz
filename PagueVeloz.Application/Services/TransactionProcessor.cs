using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Application.Services;

public sealed class TransactionProcessor : ITransactionProcessor
{
    public async Task<ProcessTransactionResponse> ProcessAsync(ProcessTransactionRequest request, CancellationToken cancellationToken = default)
    {
        return null;
    }
}