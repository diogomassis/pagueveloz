using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Application.Services;

public interface IAccountService
{
    Task<CreateAccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);
}