using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobPlatform.BuildingBlocks.Infrastructure.UnitTests;

/// <summary>messaging.IdempotencyKeys: the durable side of the Idempotency-Key header (foundation sections 3.9, 8 and 10).</summary>
public class DurableIdempotencyTests : IAsyncLifetime
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private SqliteHost _host = null!;

    public Task InitializeAsync()
    {
        _host = new SqliteHost();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private DurableIdempotencyStore<TestDbContext> NewStore() =>
        new(_host.Services.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryCacheStore(_host.Clock, Options.Create(new CacheOptions())), _host.Clock);

    [Fact]
    public async Task ReserveCompleteReplay_ThroughTheDatabase()
    {
        var store = NewStore();

        (await store.TryBeginAsync("scope", "k1", "fp", Ttl)).Should().BeTrue();
        (await store.TryBeginAsync("scope", "k1", "fp", Ttl)).Should().BeFalse("the key is reserved");
        (await store.GetAsync("scope", "k1"))!.Completed.Should().BeFalse("in flight");

        await store.CompleteAsync("scope", "k1", new IdempotencyEntry("fp", true, true, "{\"id\":1}", null), Ttl);

        var entry = await store.GetAsync("scope", "k1");
        entry.Should().NotBeNull();
        entry!.Completed.Should().BeTrue();
        entry.IsSuccess.Should().BeTrue();
        entry.PayloadJson.Should().Be("{\"id\":1}");
        entry.Fingerprint.Should().Be("fp");
    }

    [Fact]
    public async Task ACompletedKey_SurvivesACacheFlush_BecauseTheDatabaseIsTheSourceOfTruth()
    {
        await NewStore().TryBeginAsync("scope", "k", "fp", Ttl);
        await NewStore().CompleteAsync("scope", "k", new IdempotencyEntry("fp", true, true, "\"stored\"", null), Ttl);

        var afterRestart = NewStore();

        (await afterRestart.GetAsync("scope", "k"))!.PayloadJson.Should().Be("\"stored\"");
        (await afterRestart.TryBeginAsync("scope", "k", "fp", Ttl)).Should().BeFalse("a replay after a restart must not run the command twice");
    }

    [Fact]
    public async Task Release_FreesTheKey_SoAFailedRequestCanBeRetried()
    {
        var store = NewStore();
        await store.TryBeginAsync("scope", "k", "fp", Ttl);

        await store.ReleaseAsync("scope", "k");

        (await store.GetAsync("scope", "k")).Should().BeNull();
        (await store.TryBeginAsync("scope", "k", "fp", Ttl)).Should().BeTrue();
    }

    [Fact]
    public async Task Scopes_AreIsolated_SoTheSameKeyCanBeUsedByDifferentUsersAndCommands()
    {
        var store = NewStore();

        (await store.TryBeginAsync("RegisterJobSeekerAccountCommand:anonymous", "k", "fp", Ttl)).Should().BeTrue();
        (await store.TryBeginAsync("RecordPrivacyConsentCommand:anonymous", "k", "fp", Ttl)).Should().BeTrue();
    }

    [Fact]
    public async Task AnExpiredKey_NoLongerCounts_AndCanBeReserved_Again()
    {
        var store = NewStore();
        await store.TryBeginAsync("scope", "k", "fp", TimeSpan.FromMinutes(5));
        _host.Clock.Advance(TimeSpan.FromMinutes(6));

        (await store.GetAsync("scope", "k")).Should().BeNull();
        (await store.TryBeginAsync("scope", "k", "other-fp", Ttl)).Should().BeTrue();
        (await store.GetAsync("scope", "k"))!.Fingerprint.Should().Be("other-fp");
    }

    [Fact]
    public async Task AVeryLongKey_IsAcceptedAndStillDeduplicated()
    {
        var store = NewStore();
        var key = new string('x', 500);

        (await store.TryBeginAsync("scope", key, "fp", Ttl)).Should().BeTrue();
        (await store.TryBeginAsync("scope", key, "fp", Ttl)).Should().BeFalse();
        (await store.TryBeginAsync("scope", key + "y", "fp", Ttl)).Should().BeTrue("a different long key is a different key");
    }

    [Fact]
    public async Task OutboxHousekeeping_PurgesExpiredKeys_ButKeepsLiveOnes()
    {
        var store = NewStore();
        await store.TryBeginAsync("scope", "old", "fp", TimeSpan.FromMinutes(1));
        await store.TryBeginAsync("scope", "live", "fp", Ttl);
        _host.Clock.Advance(TimeSpan.FromMinutes(2));

        await _host.Services.GetRequiredService<OutboxProcessor<TestDbContext>>().ProcessBatchAsync(default);

        var keys = await _host.WithDb(db => db.Set<IdempotencyKeyRecord>().Select(r => r.Key).ToListAsync());
        keys.Should().Equal("live");
    }
}
