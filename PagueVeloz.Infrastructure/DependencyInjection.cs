using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Infrastructure.Messaging;
using PagueVeloz.Infrastructure.Persistence;
using PagueVeloz.Infrastructure.Caching;

namespace PagueVeloz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PagueVeloz");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<InMemoryAccountRepository>();
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
            services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
        }
        else
        {
            services.AddDbContext<PagueVelozDbContext>(options => options.UseNpgsql(connectionString));
            services.AddScoped<EfAccountRepository>();
            services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        }
        if (!string.IsNullOrWhiteSpace(configuration["Messaging:RabbitMq:Host"]))
        {
            services.AddSingleton<RabbitMqEventPublisher>();
            services.AddSingleton<IEventPublisher>(sp =>
            {
                var inner = sp.GetRequiredService<RabbitMqEventPublisher>();
                var cache = sp.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CircuitBreakerEventPublisher>>();
                return new CircuitBreakerEventPublisher(inner, cache, logger, configuration);
            });
        }
        else
        {
            services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        }
        // register account lock provider
        services.AddSingleton<IAccountLockProvider, InMemoryAccountLockProvider>();
        // optional Redis cache configuration
        var redisConn = configuration["Cache:Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddStackExchangeRedisCache(options => { options.Configuration = redisConn; });
            // ff ef or inmemory account implementation exists as concrete type, decorate it with cached repository
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddScoped<IAccountRepository>(sp => new CachedAccountRepository(
                    sp.GetRequiredService<EfAccountRepository>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>()));
                // prefer redis-based idempotency store when cache available
                services.AddScoped<IIdempotencyStore, RedisIdempotencyStore>();
            }
            else
            {
                services.AddScoped<IAccountRepository>(sp => new CachedAccountRepository(
                    sp.GetRequiredService<InMemoryAccountRepository>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>()));
                services.AddScoped<IIdempotencyStore, RedisIdempotencyStore>();
            }
        }
        return services;
    }
}
