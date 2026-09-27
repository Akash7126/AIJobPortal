using System.Diagnostics;
using JobPlatform.AccountIdentity.Application.Events;
using JobPlatform.AccountIdentity.Infrastructure;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.AccountIdentity.Infrastructure.IntegrationTests;

/// <summary>
/// Marks a test that needs real containers (Testcontainers). It is skipped automatically when Docker is not available,
/// so the suite stays green on machines without it. Always combine with [Trait("Category","Docker")].
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerAvailable = new(Probe);

    public DockerFactAttribute()
    {
        if (!DockerAvailable.Value)
        {
            Skip = "Docker is not available on this machine (Testcontainers tests are skipped). Install Docker to run them.";
        }
    }

    private static bool Probe()
    {
        var mode = Environment.GetEnvironmentVariable("DOCKER_TESTS");
        if (string.Equals(mode, "skip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "version --format {{.Server.Version}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(10_000))
            {
                process?.Kill();
                return false;
            }

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(process.StandardOutput.ReadToEnd());
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>An in-memory SQLite database with the real IdentityDbContext model, the outbox interceptor and the BC-03 event mapper.</summary>
public sealed class IdentityTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public IdentityTestDatabase()
    {
        _connection.Open();
        Buffer = new DomainEventBuffer();
        Correlation = new CorrelationContext();
        Interceptor = new OutboxSaveChangesInterceptor(new[] { new AccountIdentityEventMapper() }, Correlation, Buffer);
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public DomainEventBuffer Buffer { get; }
    public CorrelationContext Correlation { get; }
    public OutboxSaveChangesInterceptor Interceptor { get; }

    public IdentityDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(Interceptor)
            .ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>()
            .Options;
        return new IdentityDbContext(options);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
