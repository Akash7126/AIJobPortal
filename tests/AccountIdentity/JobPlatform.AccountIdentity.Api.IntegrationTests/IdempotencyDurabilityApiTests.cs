using System.Net;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>The Idempotency-Key header is backed by messaging.IdempotencyKeys, so a replay survives a cache flush or restart.</summary>
public class IdempotencyDurabilityApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IdempotencyDurabilityApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ARegistrationReplay_IsServedFromTheDatabase_EvenAfterTheCacheIsEmptied()
    {
        var client = new ApiClient(_factory);
        var payload = new { fullName = "Sara Ali", mobile = ApiClient.NewMobile(), email = ApiClient.NewEmail(), password = "Str0ngPass" };
        var key = Guid.NewGuid().ToString();
        var first = await client.PostAsync("/api/v1/accounts/job-seekers", payload, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var cache = _factory.Services.GetRequiredService<ICacheStore>();
        await cache.RemoveAsync($"idem:RegisterJobSeekerAccountCommand:anonymous:{key}");
        var replay = await client.PostAsync("/api/v1/accounts/job-seekers", payload, key);

        replay.StatusCode.Should().Be(HttpStatusCode.Created, "the durable record answers, not a duplicate-account conflict");
        (await replay.Json())["accountId"]!.GetValue<Guid>().Should().Be((await first.Json())["accountId"]!.GetValue<Guid>());
        var row = await _factory.WithDbAsync(db => db.Set<IdempotencyKeyRecord>().AsNoTracking().SingleAsync(r => r.Key == key));
        row.Completed.Should().BeTrue();
        row.IsSuccess.Should().BeTrue();
        row.PayloadJson.Should().NotContain("Str0ngPass", "the stored response never holds the request secrets");
    }

    [Fact]
    public async Task AFailedRegistration_DoesNotKeepTheKeyReserved()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        (await client.RegisterJobSeekerAsync(mobile)).Id.Should().NotBeEmpty();
        var key = Guid.NewGuid().ToString();

        var duplicate = await client.PostAsync("/api/v1/accounts/job-seekers",
            new { fullName = "Dup", mobile, email = ApiClient.NewEmail(), password = "Str0ngPass" }, key);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _factory.WithDbAsync(db => db.Set<IdempotencyKeyRecord>().CountAsync(r => r.Key == key))).Should().Be(0);
    }
}
