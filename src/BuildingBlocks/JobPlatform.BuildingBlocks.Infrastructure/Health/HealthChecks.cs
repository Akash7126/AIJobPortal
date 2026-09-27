using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace JobPlatform.BuildingBlocks.Infrastructure.Health;

public sealed class DbContextHealthCheck<TContext> : IHealthCheck where TContext : DbContext
{
    private readonly TContext _db;

    public DbContextHealthCheck(TContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await _db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unreachable");
}

/// <summary>Pings Redis when it is the configured cache provider; reports healthy (not applicable) for the in-memory fallback.</summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;

    public RedisHealthCheck(IServiceProvider services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_configuration["Cache:Provider"], "Redis", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("In-memory cache in use");
        }

        try
        {
            await _services.GetRequiredService<IConnectionMultiplexer>().GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable", ex);
        }
    }
}

/// <summary>Opens a channel when RabbitMQ is the configured provider; reports healthy (not applicable) for the in-memory bus.</summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;

    public RabbitMqHealthCheck(IServiceProvider services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_configuration["Messaging:Provider"], "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("In-memory bus in use");
        }

        var bus = _services.GetRequiredService<RabbitMqEventBus>();
        return await bus.IsHealthyAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("RabbitMQ unreachable");
    }
}
