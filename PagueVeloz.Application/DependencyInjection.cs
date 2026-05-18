using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Application.Services;

namespace PagueVeloz.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionProcessor, TransactionProcessor>();
        return services;
    }
}