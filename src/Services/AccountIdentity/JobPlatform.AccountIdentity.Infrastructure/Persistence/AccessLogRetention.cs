using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence;

public sealed class AccessLogOptions
{
    public const string SectionName = "AccessLog";

    /// <summary>Rows older than this are deleted (THR-048/082: retention per policy). 0 keeps everything.</summary>
    public int RetentionDays { get; set; } = 365;

    public bool Enabled { get; set; } = true;
}

/// <summary>Hourly purge of identity.AccessLog beyond the configured retention. BC-07 holds the long-term audit trail from the published events.</summary>
public sealed class AccessLogRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly AccessLogOptions _options;
    private readonly ILogger<AccessLogRetentionService> _logger;

    public AccessLogRetentionService(IServiceScopeFactory scopes, TimeProvider clock, IOptions<AccessLogOptions> options,
        ILogger<AccessLogRetentionService> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || _options.RetentionDays <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), _clock);
        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Access log retention purge failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Deletes access-log rows older than the retention window; returns how many were removed. Public so tests can drive it.</summary>
    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        if (_options.RetentionDays <= 0)
        {
            return 0;
        }

        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-_options.RetentionDays);
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var removed = await db.AccessLog.Where(l => l.AtUtc < cutoff).ExecuteDeleteAsync(ct);
        if (removed > 0)
        {
            _logger.LogInformation("Access log retention removed {Count} rows older than {Cutoff:O}", removed, cutoff);
        }

        return removed;
    }
}
