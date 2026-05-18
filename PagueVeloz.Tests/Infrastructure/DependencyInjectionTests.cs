using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Infrastructure;
using PagueVeloz.Infrastructure.Caching;
using PagueVeloz.Infrastructure.Messaging;
using PagueVeloz.Infrastructure.Persistence;

namespace PagueVeloz.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_registers_expected_services_for_inmemory_configuration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            // no connection string, no messaging or cache
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        // InMemory implementations should be registered
        var inMemoryRepo = provider.GetService<InMemoryAccountRepository>();
        Assert.NotNull(inMemoryRepo);
        var idemp = provider.GetService<PagueVeloz.Application.Abstractions.IIdempotencyStore>();
        Assert.NotNull(idemp);
        var uow = provider.GetService<PagueVeloz.Application.Abstractions.IUnitOfWork>();
        Assert.NotNull(uow);
        var publisher = provider.GetService<IEventPublisher>();
        Assert.NotNull(publisher);
        Assert.IsType<PagueVeloz.Infrastructure.InMemoryEventPublisher>(publisher);
        var lockProvider = provider.GetService<PagueVeloz.Application.Abstractions.IAccountLockProvider>();
        Assert.NotNull(lockProvider);
    }
}
