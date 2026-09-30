using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.TestSupport;

/// <summary>An in-memory SQLite database with a real BC DbContext model, the outbox interceptor and the BC's domain-event mapper.</summary>
public sealed class SqliteTestDatabase<TContext> : IAsyncDisposable where TContext : BaseDbContext
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Func<DbContextOptions<TContext>, TContext> _factory;

    public SqliteTestDatabase(Func<DbContextOptions<TContext>, TContext> factory, params IDomainEventMapper[] mappers)
    {
        _factory = factory;
        _connection.Open();
        Buffer = new DomainEventBuffer();
        Correlation = new CorrelationContext();
        Interceptor = new OutboxSaveChangesInterceptor(mappers, Correlation, Buffer);
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public DomainEventBuffer Buffer { get; }
    public CorrelationContext Correlation { get; }
    public OutboxSaveChangesInterceptor Interceptor { get; }

    public TContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(_connection)
            .AddInterceptors(Interceptor)
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>()
            .Options;
        return _factory(options);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
