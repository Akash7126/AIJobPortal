using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Infrastructure.IntegrationTests;

/// <summary>THR-048/082: the access log is kept for the configured retention and purged after that.</summary>
public class AccessLogRetentionTests : IAsyncLifetime
{
    private IdentityTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new IdentityTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private AccessLogRetentionService Service(int retentionDays, bool enabled = true)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _db.NewContext());
        return new AccessLogRetentionService(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _db.Clock,
            Options.Create(new AccessLogOptions { RetentionDays = retentionDays, Enabled = enabled }), NullLogger<AccessLogRetentionService>.Instance);
    }

    private async Task SeedAsync(params int[] ageInDays)
    {
        await using var context = _db.NewContext();
        foreach (var age in ageInDays)
        {
            context.AccessLog.Add(new AccessLogRecord
            {
                Id = Guid.NewGuid(), AtUtc = _db.Clock.GetUtcNow().UtcDateTime.AddDays(-age), Action = "auth.login", Resource = "auth", Decision = "Allow"
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<int> CountAsync()
    {
        await using var context = _db.NewContext();
        return await context.AccessLog.CountAsync();
    }

    [Fact]
    public async Task Purge_RemovesOnlyRowsOlderThanTheRetention()
    {
        await SeedAsync(1, 100, 364, 366, 900);

        var removed = await Service(365).PurgeAsync(default);

        removed.Should().Be(2);
        (await CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Purge_WithRetentionZero_KeepsEverything()
    {
        await SeedAsync(1, 900);

        (await Service(0).PurgeAsync(default)).Should().Be(0);

        (await CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Purge_IsIdempotent()
    {
        await SeedAsync(500);
        var service = Service(30);

        (await service.PurgeAsync(default)).Should().Be(1);
        (await service.PurgeAsync(default)).Should().Be(0);
    }

    [Fact]
    public async Task TheHostedService_RunsAnInitialPurgeOnStart_WhenEnabled()
    {
        await SeedAsync(400, 2);
        var service = Service(365);

        await service.StartAsync(default);
        await Task.Delay(300);
        await service.StopAsync(default);

        (await CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TheHostedService_DoesNothing_WhenDisabled()
    {
        await SeedAsync(400);
        var service = Service(365, enabled: false);

        await service.StartAsync(default);
        await Task.Delay(100);
        await service.StopAsync(default);

        (await CountAsync()).Should().Be(1);
    }
}
