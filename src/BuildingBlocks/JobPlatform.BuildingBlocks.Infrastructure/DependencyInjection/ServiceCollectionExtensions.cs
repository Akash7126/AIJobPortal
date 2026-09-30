using System.Reflection;
using FluentValidation;
using JobPlatform.BuildingBlocks.Infrastructure.Behaviors;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Cqrs;
using JobPlatform.BuildingBlocks.Infrastructure.Health;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers dispatcher, pipeline behaviors (Logging, RateLimit, Validation, Authorization, Idempotency, UnitOfWork), correlation,
    /// cache provider (Cache:Provider = Redis|InMemory), rate limiter, idempotency store and event bus (Messaging:Provider = RabbitMq|InMemory).
    /// </summary>
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ISender, Sender>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(RateLimitBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());
        services.AddScoped<DomainEventBuffer>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<OutboxSaveChangesInterceptor>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.Configure<CacheOptions>(configuration.GetSection("Cache"));
        // Providers are chosen lazily from the final configuration so host-level overrides (tests, containers) always win.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = ConfigurationOptions.Parse(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheOptions>>().Value.ConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 2000;
            options.SyncTimeout = 500;
            options.AsyncTimeout = 500;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<InMemoryCacheStore>();
        services.AddSingleton<RedisCacheStore>();
        services.AddSingleton<ICacheStore>(sp => IsProvider(sp, "Cache:Provider", "Redis")
            ? sp.GetRequiredService<RedisCacheStore>()
            : sp.GetRequiredService<InMemoryCacheStore>());

        services.AddSingleton<IRateLimiter, CacheRateLimiter>();
        services.AddSingleton<IIdempotencyStore, CacheIdempotencyStore>();

        services.Configure<RabbitMqOptions>(configuration.GetSection("Messaging:RabbitMq"));
        services.AddSingleton<RabbitMqEventBus>();
        services.AddSingleton<InMemoryEventBus>();
        services.AddSingleton<IIntegrationEventBus>(sp => IsProvider(sp, "Messaging:Provider", "RabbitMq")
            ? sp.GetRequiredService<RabbitMqEventBus>()
            : sp.GetRequiredService<InMemoryEventBus>());

        return services;
    }

    /// <summary>Registers every IRequestHandler and IValidator found in the assembly (handlers may be internal).</summary>
    public static IServiceCollection AddRequestHandlersFrom(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                {
                    continue;
                }

                var definition = iface.GetGenericTypeDefinition();
                if (definition == typeof(IRequestHandler<,>))
                {
                    services.AddScoped(iface, type);
                }
                else if (definition == typeof(IDomainEventHandler<>))
                {
                    services.AddScoped(iface, type);
                }
            }
        }

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
        return services;
    }

    /// <summary>
    /// Replaces the cache-only idempotency store by the durable one (messaging.IdempotencyKeys, cache as fast path).
    /// Call after AddBuildingBlocks; the context must derive from BaseDbContext so the table is part of its model.
    /// </summary>
    public static IServiceCollection AddDurableIdempotency<TContext>(this IServiceCollection services) where TContext : BaseDbContext
    {
        services.RemoveAll<IIdempotencyStore>();
        services.AddSingleton<IIdempotencyStore, DurableIdempotencyStore<TContext>>();
        return services;
    }

    public static IServiceCollection AddOutboxProcessor<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));
        services.AddSingleton<OutboxProcessor<TContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor<TContext>>());
        return services;
    }

    public static IServiceCollection AddInboxProcessor<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        services.Configure<InboxOptions>(configuration.GetSection("Inbox"));
        services.TryAddSingleton(sp =>
        {
            var registry = new InboxHandlerRegistry();
            foreach (var seed in sp.GetServices<InboxRegistrationSeed>())
            {
                registry.Add(seed.Registration);
            }

            return registry;
        });
        services.AddSingleton<IInboxWriter, EfInboxWriter<TContext>>();
        services.AddSingleton<InboxProcessor<TContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<InboxProcessor<TContext>>());
        return services;
    }

    /// <summary>Subscribes an inbox handler for an integration event (consumer = consuming BC slug, eventType = catalogue name, e.g. AccountApproved).</summary>
    public static IServiceCollection AddInboxHandler<TEvent, THandler>(this IServiceCollection services, string consumerName, string eventType)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new InboxRegistrationSeed(new InboxRegistration(consumerName, eventType, typeof(TEvent), typeof(THandler))));
        return services;
    }

    internal static bool IsProvider(IServiceProvider sp, string key, string expected) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[key], expected, StringComparison.OrdinalIgnoreCase);

    public static IHealthChecksBuilder AddBuildingBlockHealthChecks<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        var builder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
            .AddCheck<DbContextHealthCheck<TContext>>("database", tags: new[] { "ready" });
        builder.AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });
        builder.AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });
        return builder;
    }
}

/// <summary>Carries an inbox registration through DI so the registry is filled when the host starts.</summary>
public sealed record InboxRegistrationSeed(InboxRegistration Registration);
