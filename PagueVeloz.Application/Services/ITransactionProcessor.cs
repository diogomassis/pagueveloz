using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Application.Services;

public interface ITransactionProcessor
{
    Task<ProcessTransactionResponse> ProcessAsync(ProcessTransactionRequest request, CancellationToken cancellationToken = default);
}