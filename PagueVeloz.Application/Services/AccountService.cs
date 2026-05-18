using PagueVeloz.Application.Dtos;

namespace PagueVeloz.Application.Services;

public sealed class AccountService : IAccountService
{
    public async Task<CreateAccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        return null;
    }
}
