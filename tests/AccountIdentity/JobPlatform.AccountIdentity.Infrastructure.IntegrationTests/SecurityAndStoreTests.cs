using System.Diagnostics;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.AccountIdentity.Infrastructure.Caching;
using JobPlatform.AccountIdentity.Infrastructure.Delivery;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.AccountIdentity.Infrastructure.IntegrationTests;

public class CryptographyTests
{
    private static IOptions<SecurityOptions> Options(int iterations = 1000) => Microsoft.Extensions.Options.Options.Create(new SecurityOptions
    {
        MasterKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()),
        Pepper = "pepper",
        Pbkdf2Iterations = iterations
    });

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-04")]
    public void PasswordHasher_ProducesSaltedSelfDescribingHashes_ThatVerifyOnlyTheRightPassword()
    {
        var hasher = new Pbkdf2PasswordHasher(Options());

        var first = hasher.Hash("Str0ngPass");
        var second = hasher.Hash("Str0ngPass");

        first.Should().StartWith("$pbkdf2-sha512$i=1000$").And.NotContain("Str0ngPass");
        first.Should().NotBe(second, "every hash has its own salt");
        hasher.Verify("Str0ngPass", first).Should().BeTrue();
        hasher.Verify("str0ngpass", first).Should().BeFalse();
        hasher.Verify("Str0ngPass", "garbage").Should().BeFalse();
        hasher.Verify("Str0ngPass", "$pbkdf2-sha512$i=abc$x$y").Should().BeFalse();
        hasher.Verify("Str0ngPass", "$pbkdf2-sha512$i=1000$!!!$???").Should().BeFalse();
    }

    [Fact]
    public void PasswordHasher_FlagsHashesMadeWithWeakerParameters_ForRehashOnNextLogin()
    {
        var weak = new Pbkdf2PasswordHasher(Options(1000)).Hash("pw");
        var strong = new Pbkdf2PasswordHasher(Options(5000));

        strong.NeedsRehash(weak).Should().BeTrue();
        strong.NeedsRehash(strong.Hash("pw")).Should().BeFalse();
        strong.NeedsRehash("legacy-format").Should().BeTrue();
        strong.Verify("pw", weak).Should().BeTrue("old hashes keep verifying so users are not locked out");
        strong.SimulateVerify("anything");
    }

    [Fact]
    public void PasswordHasher_DefaultIterations_StayWithinAnInteractiveBudget()
    {
        // Performance smoke test (handover section 10): the production cost factor must not blow the 3 s registration budget.
        var hasher = new Pbkdf2PasswordHasher(Options(new SecurityOptions().Pbkdf2Iterations));
        var stopwatch = Stopwatch.StartNew();

        var hash = hasher.Hash("Str0ngPass");
        hasher.Verify("Str0ngPass", hash).Should().BeTrue();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "hash + verify at the default cost, even while the whole suite competes for the CPU");
    }

    [Fact]
    public void SecretHasher_IsKeyedAndConstantTime_AndRejectsGarbage()
    {
        var hasher = new HmacSecretHasher(Options());
        var other = new HmacSecretHasher(Microsoft.Extensions.Options.Options.Create(new SecurityOptions { Pepper = "different" }));

        var hash = hasher.Hash("123456");

        hasher.Verify("123456", hash).Should().BeTrue();
        hasher.Verify("123457", hash).Should().BeFalse();
        other.Verify("123456", hash).Should().BeFalse("a different pepper yields a different hash");
        hasher.Verify("123456", "not base64!").Should().BeFalse();
        hash.Should().NotContain("123456");
    }

    [Fact]
    public void SecretGenerator_ProducesSixDigitCodesAndUnpredictableTokens()
    {
        var generator = new SecretGenerator();

        Enumerable.Range(0, 200).Select(_ => generator.GenerateOtp()).Should().OnlyContain(c => c.Length == 6 && c.All(char.IsAsciiDigit));
        Enumerable.Range(0, 200).Select(_ => generator.GenerateOtp()).Distinct().Count().Should().BeGreaterThan(100);
        generator.GenerateApiKeyId().Should().StartWith("jp_");
        generator.GenerateApiSecret().Should().StartWith("jps_").And.HaveLength(47);
        generator.GenerateToken().Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        generator.GenerateToken().Should().NotBe(generator.GenerateToken());
    }

    [Fact]
    public void AesGcmProtector_RoundTrips_AndDetectsTampering()
    {
        var protector = new AesGcmProtector(Options());
        var secret = System.Text.Encoding.UTF8.GetBytes("JBSWY3DPEHPK3PXP");

        var protectedValue = protector.Protect(secret);

        protectedValue.Should().NotContain("JBSWY3DP");
        protector.Unprotect(protectedValue).Should().Equal(secret);
        protector.Protect(secret).Should().NotBe(protectedValue, "each encryption uses a fresh nonce");
        var bytes = Convert.FromBase64String(protectedValue);
        bytes[^1] ^= 0xFF;
        var tampered = () => protector.Unprotect(Convert.ToBase64String(bytes));
        tampered.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("c2hvcnQ=")]
    public void AesGcmProtector_RequiresA32ByteMasterKey(string key)
    {
        var create = () => new AesGcmProtector(Microsoft.Extensions.Options.Options.Create(new SecurityOptions { MasterKey = key }));

        create.Should().Throw<InvalidOperationException>().WithMessage("*32-byte*");
    }

    [Fact]
    public void Totp_MatchesTheRfc6238TestVector_AndAcceptsOnlyAdjacentSteps()
    {
        const string rfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"; // ASCII "12345678901234567890"
        var service = new TotpMfaService(new AesGcmProtector(Options()));
        var at59 = DateTimeOffset.FromUnixTimeSeconds(59).UtcDateTime;

        TotpMfaService.ComputeCode(rfcSecret, at59).Should().Be("287082", "RFC 6238 appendix B: 94287082 truncated to six digits");
        service.VerifyCode(rfcSecret, "287082", at59).Should().BeTrue();
        service.VerifyCode(rfcSecret, "287082", at59.AddSeconds(30)).Should().BeTrue("one step of clock drift is tolerated");
        service.VerifyCode(rfcSecret, "287082", at59.AddSeconds(120)).Should().BeFalse();
        service.VerifyCode(rfcSecret, "12345", at59).Should().BeFalse();
        service.VerifyCode(rfcSecret, "abcdef", at59).Should().BeFalse();
    }

    [Fact]
    public void Totp_SecretsAreBase32_ProtectedAtRest_AndTheProvisioningUriIsStandard()
    {
        var service = new TotpMfaService(new AesGcmProtector(Options()));

        var secret = service.GenerateSecret();
        var protectedSecret = service.Protect(secret);

        secret.Should().MatchRegex("^[A-Z2-7]{32}$");
        protectedSecret.Should().NotContain(secret);
        service.Unprotect(protectedSecret).Should().Be(secret);
        var uri = service.BuildProvisioningUri("JobPlatform", "a@b.com", secret);
        uri.Should().StartWith("otpauth://totp/JobPlatform:a%40b.com?secret=" + secret).And.Contain("digits=6").And.Contain("period=30");
        service.VerifyCode(secret, TotpMfaService.ComputeCode(secret, DateTime.UtcNow), DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void ServiceClientRegistry_AuthenticatesConfiguredClientsOnly()
    {
        var registry = new ConfiguredServiceClientRegistry(Microsoft.Extensions.Options.Options.Create(new ServiceClientOptions
        {
            Clients = { new ServiceClientEntry { ClientId = "svc-a", Secret = "s3cret" }, new ServiceClientEntry { ClientId = "", Secret = "x" } }
        }));

        registry.Authenticate("svc-a", "s3cret")!.Scopes.Should().Contain(Scopes.Internal);
        registry.Authenticate("svc-a", "wrong").Should().BeNull();
        registry.Authenticate("svc-b", "s3cret").Should().BeNull();
        registry.Authenticate("", "x").Should().BeNull();
    }

    [Fact]
    public async Task LoggingSender_NeverWritesTheCode_UnlessTheExplicitDevelopmentFlagIsOn()
    {
        var quiet = new ListLogger();
        var loud = new ListLogger();
        var mobile = MobileNumber.Create("+970591111111");
        var email = Email.Create("a@b.com");

        var off = new LoggingMessageSender(quiet, Microsoft.Extensions.Options.Options.Create(new DeliveryOptions()));
        await off.SendActivationCodeAsync(mobile, "424242", Language.En);
        await off.SendVerificationAsync(email, Guid.NewGuid(), "verify-token-xyz", Language.En);
        await off.SendLoginCodeAsync(email, "737373", Language.Ar);
        var on = new LoggingMessageSender(loud, Microsoft.Extensions.Options.Options.Create(new DeliveryOptions { LogCodesInDevelopment = true }));
        await on.SendActivationCodeAsync(mobile, "424242", Language.En);

        quiet.Lines.Should().NotBeEmpty().And.OnlyContain(l => !l.Contains("424242") && !l.Contains("verify-token-xyz") && !l.Contains("737373") && !l.Contains("591111"));
        loud.Lines.Should().Contain(l => l.Contains("424242"), "only the dev flag exposes the code, and only at Debug level");
    }

    [Fact]
    public async Task CapturingSender_KeepsMessagesForTests()
    {
        var sender = new CapturingMessageSender();

        await sender.SendActivationCodeAsync(MobileNumber.Create("+970591111111"), "111111", Language.Ar);
        await sender.SendActivationCodeAsync(MobileNumber.Create("+970591111111"), "222222", Language.En);

        sender.LastFor("activation-code", "+970591111111")!.Code.Should().Be("222222");
        sender.Messages.Should().HaveCount(2);
        sender.LastFor("activation-code", "nobody").Should().BeNull();
    }

    private sealed class ListLogger : ILogger<LoggingMessageSender>
    {
        public List<string> Lines { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add($"{logLevel}: {formatter(state, exception)}");
    }
}

public class StoreTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private InMemoryCacheStore Cache() => new(_clock, Microsoft.Extensions.Options.Options.Create(new CacheOptions()));

    [Fact]
    public async Task SessionStore_CreateGetSaveAndSlidingTtl()
    {
        var cache = Cache();
        var store = new CacheSessionStore(cache, _clock);
        var session = UserSession.Create(Guid.NewGuid(), 30, "h", _clock);

        await store.CreateAsync(session);
        (await store.GetAsync(session.SessionId))!.AccountId.Should().Be(session.AccountId);

        _clock.Advance(TimeSpan.FromMinutes(20));
        var loaded = (await store.GetAsync(session.SessionId))!;
        loaded.Touch(_clock);
        await store.SaveAsync(loaded);
        _clock.Advance(TimeSpan.FromMinutes(20));
        (await store.GetAsync(session.SessionId)).Should().NotBeNull("activity re-armed the idle TTL");

        _clock.Advance(TimeSpan.FromMinutes(31));
        (await store.GetAsync(session.SessionId)).Should().BeNull("the idle window elapsed");
    }

    [Fact]
    public async Task SessionStore_InvalidateAll_EndsEverySessionOfTheAccountExceptTheOneKept()
    {
        var store = new CacheSessionStore(Cache(), _clock);
        var account = Guid.NewGuid();
        var kept = UserSession.Create(account, 30, "h", _clock);
        var ended = UserSession.Create(account, 30, "h", _clock);
        var foreign = UserSession.Create(Guid.NewGuid(), 30, "h", _clock);
        foreach (var session in new[] { kept, ended, foreign })
        {
            await store.CreateAsync(session);
        }

        await store.InvalidateAllForAccountAsync(account, kept.SessionId);

        (await store.GetAsync(kept.SessionId))!.Status.Should().Be(SessionStatus.Active);
        (await store.GetAsync(ended.SessionId))!.Status.Should().Be(SessionStatus.Invalidated);
        (await store.GetAsync(foreign.SessionId))!.Status.Should().Be(SessionStatus.Active);
        await store.InvalidateAllForAccountAsync(account);
        (await store.GetAsync(kept.SessionId))!.Status.Should().Be(SessionStatus.Invalidated);
    }

    [Fact]
    public async Task SessionStore_RevokedTokensAndClientsExpire()
    {
        var store = new CacheSessionStore(Cache(), _clock);

        await store.RevokeTokenAsync("jti-1", TimeSpan.FromMinutes(15));
        await store.RevokeClientAsync("client-1", TimeSpan.FromHours(1));

        (await store.IsTokenRevokedAsync("jti-1")).Should().BeTrue();
        (await store.IsTokenRevokedAsync("jti-2")).Should().BeFalse();
        (await store.IsClientRevokedAsync("client-1")).Should().BeTrue();
        _clock.Advance(TimeSpan.FromMinutes(16));
        (await store.IsTokenRevokedAsync("jti-1")).Should().BeFalse("a token cannot outlive its own lifetime anyway");
        (await store.IsClientRevokedAsync("client-1")).Should().BeTrue();
    }

    [Fact]
    public async Task MfaChallengeStore_IsSingleUseAndShortLived_AndNeverStoresTheRawToken()
    {
        var cache = Cache();
        var store = new CacheMfaChallengeStore(cache, new SecretGenerator(), _clock);
        var account = Guid.NewGuid();

        var challenge = await store.CreateAsync(account);

        (await store.GetAccountIdAsync(challenge.Token)).Should().Be(account);
        (await cache.GetAsync($"mfa-challenge:{challenge.Token}")).Should().BeNull("only a hash of the token is a cache key");
        challenge.ExpiresAtUtc.Should().Be(_clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        await store.DeleteAsync(challenge.Token);
        (await store.GetAccountIdAsync(challenge.Token)).Should().BeNull();
        var second = await store.CreateAsync(account);
        _clock.Advance(TimeSpan.FromMinutes(6));
        (await store.GetAccountIdAsync(second.Token)).Should().BeNull();
        (await store.GetAccountIdAsync("unknown")).Should().BeNull();
    }

    [Fact]
    public async Task LoginCodeStore_CodesAreSingleUse_AttemptCappedAndExpire()
    {
        var hasher = new HmacSecretHasher(Microsoft.Extensions.Options.Options.Create(new SecurityOptions { Pepper = "p" }));
        var store = new CacheLoginCodeStore(Cache());
        var account = Guid.NewGuid();

        await store.StoreAsync(account, hasher.Hash("111111"), TimeSpan.FromMinutes(10));
        (await store.VerifyAndConsumeAsync(account, "000000", hasher, 5)).Should().BeFalse();
        (await store.VerifyAndConsumeAsync(account, "111111", hasher, 5)).Should().BeTrue();
        (await store.VerifyAndConsumeAsync(account, "111111", hasher, 5)).Should().BeFalse("single use");

        await store.StoreAsync(account, hasher.Hash("222222"), TimeSpan.FromMinutes(10));
        for (var i = 0; i < 5; i++)
        {
            await store.VerifyAndConsumeAsync(account, "000000", hasher, 5);
        }

        (await store.VerifyAndConsumeAsync(account, "222222", hasher, 5)).Should().BeFalse("the attempt cap burned the code");
        await store.StoreAsync(account, hasher.Hash("333333"), TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(2));
        (await store.VerifyAndConsumeAsync(account, "333333", hasher, 5)).Should().BeFalse();
    }

    [Fact]
    public async Task RoleDirectory_ResolvesPermissionsFromTheCachedMap_AndPicksUpChangesAfterInvalidation()
    {
        await using var database = new IdentityTestDatabase();
        await using var context = database.NewContext();
        var admin = JobPlatform.AccountIdentity.Domain.Accounts.Account.CreateAdministrator("A", Email.Create("a@b.com"), MobileNumber.Create("+970591111111"),
            new JobPlatform.AccountIdentity.Domain.Accounts.PasswordHash("h"), 1, _clock);
        context.Accounts.Add(admin);
        context.Roles.Add(Role.Create(WellKnownRoles.Administrator, "Administrator", true, new[] { Permissions.AccountsRead }));
        await context.SaveChangesAsync();
        var cache = Cache();
        var directory = new CachedRoleDirectory(context, cache, NullLogger<CachedRoleDirectory>.Instance);
        var invalidator = new CacheInvalidator(cache);

        var first = await directory.GetRolesForAccountAsync(admin.Id.Value);
        first.Should().ContainSingle().Which.Permissions.Should().Equal(Permissions.AccountsRead);

        var role = await context.Roles.SingleAsync();
        role.GrantPermission(JobPlatform.AccountIdentity.Domain.Common.Actor.Administrator(Guid.NewGuid()), Permissions.AccountsBan, _clock);
        await context.SaveChangesAsync();
        (await directory.GetRolesForAccountAsync(admin.Id.Value)).Single().Permissions.Should().NotContain(Permissions.AccountsBan, "the cache still holds the old map");

        await invalidator.InvalidateRoleMapAsync();
        (await directory.GetRolesForAccountAsync(admin.Id.Value)).Single().Permissions.Should().Contain(Permissions.AccountsBan);

        await invalidator.InvalidateAccountRolesAsync(admin.Id.Value);
        await invalidator.InvalidatePasswordPolicyAsync();
        (await directory.GetRolesForAccountAsync(Guid.NewGuid())).Should().BeEmpty();
    }

    [Fact]
    public async Task RoleDirectory_FallsBackToTheDatabaseWhenTheCacheFails()
    {
        await using var database = new IdentityTestDatabase();
        await using var context = database.NewContext();
        var admin = JobPlatform.AccountIdentity.Domain.Accounts.Account.CreateAdministrator("A", Email.Create("a@b.com"), MobileNumber.Create("+970591111111"),
            new JobPlatform.AccountIdentity.Domain.Accounts.PasswordHash("h"), 1, _clock);
        context.Accounts.Add(admin);
        context.Roles.Add(Role.Create(WellKnownRoles.Administrator, "Administrator", true, new[] { Permissions.AccountsRead }));
        await context.SaveChangesAsync();
        var directory = new CachedRoleDirectory(context, new BrokenCache(), NullLogger<CachedRoleDirectory>.Instance);

        (await directory.GetRolesForAccountAsync(admin.Id.Value)).Should().ContainSingle("a cache outage degrades to a database read, not an error");
    }

    private sealed class BrokenCache : ICacheStore
    {
        private static Exception Down() => new InvalidOperationException("redis down");

        public Task<string?> GetAsync(string key, CancellationToken ct = default) => throw Down();
        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) => throw Down();
        public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) => throw Down();
        public Task<bool> RemoveAsync(string key, CancellationToken ct = default) => throw Down();
        public Task<long> IncrementAsync(string key, TimeSpan ttlOnCreate, CancellationToken ct = default) => throw Down();
        public Task<bool> RefreshTtlAsync(string key, TimeSpan ttl, CancellationToken ct = default) => throw Down();
        public Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default) => throw Down();
        public Task SetAddAsync(string setKey, string member, TimeSpan ttl, CancellationToken ct = default) => throw Down();
        public Task<IReadOnlyCollection<string>> SetMembersAsync(string setKey, CancellationToken ct = default) => throw Down();
        public Task SetRemoveAsync(string setKey, string member, CancellationToken ct = default) => throw Down();
    }
}
