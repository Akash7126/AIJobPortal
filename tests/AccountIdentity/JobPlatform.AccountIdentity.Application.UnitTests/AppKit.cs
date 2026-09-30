using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

/// <summary>Deterministic "h:" hashing for passwords, codes and secrets (no crypto in unit tests).</summary>
internal sealed class FakeHasher : IPasswordHasher, IOtpHasher, IApiSecretHasher
{
    public int SimulateCalls { get; private set; }

    public string Hash(string value) => (RehashNeeded ? "h2:" : "h:") + value;

    public bool Verify(string secret, string hash) => hash == "h:" + secret || hash == "h2:" + secret;

    public bool RehashNeeded { get; set; }

    public bool NeedsRehash(string hash) => RehashNeeded && !hash.StartsWith("h2:");

    public void SimulateVerify(string password) => SimulateCalls++;
}

internal sealed class FakeGenerator : ISecretGenerator
{
    public string Otp { get; set; } = "123456";

    public string GenerateOtp() => Otp;

    public string GenerateApiKeyId() => "jp_key";

    public string GenerateApiSecret() => "jps_secret";

    public string GenerateToken() => "token-" + Guid.NewGuid().ToString("N")[..6];
}

internal sealed class InMemorySessionStore : ISessionStore
{
    public Dictionary<Guid, SessionSnapshot> Sessions { get; } = new();
    public HashSet<string> RevokedTokens { get; } = new();
    public HashSet<string> RevokedClients { get; } = new();
    public List<Guid> InvalidatedAccounts { get; } = new();

    public Task CreateAsync(UserSession session, CancellationToken ct = default) => SaveAsync(session, ct);

    public Task<UserSession?> GetAsync(Guid sessionId, CancellationToken ct = default) =>
        Task.FromResult(Sessions.TryGetValue(sessionId, out var s) ? UserSession.Rehydrate(s) : null);

    public Task SaveAsync(UserSession session, CancellationToken ct = default)
    {
        Sessions[session.SessionId] = session.ToSnapshot();
        return Task.CompletedTask;
    }

    public Task InvalidateAllForAccountAsync(Guid accountId, Guid? exceptSessionId = null, CancellationToken ct = default)
    {
        InvalidatedAccounts.Add(accountId);
        foreach (var (id, s) in Sessions.ToList().Where(p => p.Value.AccountId == accountId && p.Key != exceptSessionId))
        {
            Sessions[id] = s with { Status = SessionStatus.Invalidated };
        }

        return Task.CompletedTask;
    }

    public Task RevokeTokenAsync(string tokenId, TimeSpan ttl, CancellationToken ct = default)
    {
        RevokedTokens.Add(tokenId);
        return Task.CompletedTask;
    }

    public Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken ct = default) => Task.FromResult(RevokedTokens.Contains(tokenId));

    public Task RevokeClientAsync(string clientId, TimeSpan ttl, CancellationToken ct = default)
    {
        RevokedClients.Add(clientId);
        return Task.CompletedTask;
    }

    public Task<bool> IsClientRevokedAsync(string clientId, CancellationToken ct = default) => Task.FromResult(RevokedClients.Contains(clientId));
}

internal sealed class InMemoryMfaChallenges : IMfaChallengeStore
{
    public Dictionary<string, Guid> Tokens { get; } = new();

    public Task<MfaChallengeToken> CreateAsync(Guid accountId, CancellationToken ct = default)
    {
        var token = "mfa-" + Tokens.Count;
        Tokens[token] = accountId;
        return Task.FromResult(new MfaChallengeToken(token, DateTime.UtcNow.AddMinutes(5)));
    }

    public Task<Guid?> GetAccountIdAsync(string token, CancellationToken ct = default) =>
        Task.FromResult(Tokens.TryGetValue(token, out var id) ? id : (Guid?)null);

    public Task DeleteAsync(string token, CancellationToken ct = default)
    {
        Tokens.Remove(token);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryLoginCodes : ILoginCodeStore
{
    public Dictionary<Guid, string> Hashes { get; } = new();

    public Task StoreAsync(Guid accountId, string codeHash, TimeSpan ttl, CancellationToken ct = default)
    {
        Hashes[accountId] = codeHash;
        return Task.CompletedTask;
    }

    public Task<bool> VerifyAndConsumeAsync(Guid accountId, string code, ISecretVerifier verifier, int maxAttempts, CancellationToken ct = default)
    {
        var ok = Hashes.TryGetValue(accountId, out var hash) && verifier.Verify(code, hash);
        if (ok)
        {
            Hashes.Remove(accountId);
        }

        return Task.FromResult(ok);
    }
}

internal sealed class RecordingAccessLog : IAccessLog
{
    public List<AccessLogEntry> Entries { get; } = new();

    public Task AppendAsync(AccessLogEntry entry, CancellationToken ct = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>Repository fake: aggregates are added and looked up in memory.</summary>
internal sealed class InMemoryAccounts : IAccountRepository
{
    public List<Account> Items { get; } = new();

    public Task<Account?> GetByIdAsync(AccountId id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<Account>> FindByLoginAsync(string login, ActorType? actorType, CancellationToken ct = default)
    {
        IEnumerable<Account> query = Items;
        query = login.Contains('@')
            ? query.Where(a => a.Email?.Value == login.ToLowerInvariant())
            : query.Where(a => a.Mobile.Value == login);
        if (actorType is { } type)
        {
            query = query.Where(a => a.ActorType == type);
        }

        return Task.FromResult<IReadOnlyList<Account>>(query.ToList());
    }

    public void Add(Account account) => Items.Add(account);
}

internal static class AppKit
{
    public const string Password = "Str0ngPass";

    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(Guid? id = null, ActorType? actor = null, string source = "src-1", bool authenticated = true, bool mfa = false,
        Guid? sessionId = null, string? tokenId = null, IEnumerable<string>? scopes = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(authenticated);
        user.UserId.Returns(id);
        user.ActorType.Returns(actor);
        user.SourceKey.Returns(source);
        user.IpAddress.Returns("203.0.113.9");
        user.Language.Returns(Language.En);
        user.MfaVerified.Returns(mfa);
        user.SessionId.Returns(sessionId);
        user.TokenId.Returns(tokenId);
        user.Scopes.Returns((scopes ?? Array.Empty<string>()).ToArray());
        user.RoleIds.Returns(Array.Empty<Guid>());
        return user;
    }

    public static PasswordPolicy Policy(TimeProvider clock) => PasswordPolicy.CreateDefault(clock);

    public static IPasswordPolicyRepository PolicyRepo(TimeProvider clock, PasswordPolicy? policy = null)
    {
        var repo = Substitute.For<IPasswordPolicyRepository>();
        repo.GetAsync(Arg.Any<CancellationToken>()).Returns(policy ?? Policy(clock));
        return repo;
    }

    public static ISessionTimeoutSettingRepository TimeoutRepo(TimeProvider clock, SessionTimeoutSetting? setting = null)
    {
        var repo = Substitute.For<ISessionTimeoutSettingRepository>();
        repo.GetAsync(Arg.Any<CancellationToken>()).Returns(setting ?? SessionTimeoutSetting.CreateDefault(clock));
        return repo;
    }

    public static Actor Admin() => Actor.Administrator(Guid.NewGuid());

    public static Account Pending(TimeProvider clock, ActorType type = ActorType.JobSeeker, string mobile = "+970591111111", string? email = "u@example.com")
    {
        var identity = type == ActorType.JobSeeker ? null : ExternalIdentityKey.Create("C-1");
        var account = Account.Register(new RegistrationDetails(type, "User", email is null ? null : Email.Create(email), MobileNumber.Create(mobile), identity),
            new PasswordHash("h:" + Password), clock);
        account.ClearDomainEvents();
        return account;
    }

    public static Account Active(TimeProvider clock, ActorType type = ActorType.JobSeeker, string mobile = "+970591111111", string? email = "u@example.com")
    {
        var account = Pending(clock, type, mobile, email);
        if (type == ActorType.ExternalJobSite)
        {
            account.ApproveByStaff(Actor.AuthorisedStaff(Guid.NewGuid()), clock);
        }
        else
        {
            account.IssueActivationChallenge("h:123456", clock);
            account.Activate("123456", new FakeHasher(), clock);
        }

        account.ClearDomainEvents();
        return account;
    }

    public static Error ErrorOf<T>(Result<T> result)
    {
        result.IsFailure.Should().BeTrue("the operation was expected to fail");
        return result.Error!;
    }

    public static IssuedAccessToken Token(string value = "jwt", string id = "jti-1") => new(value, id, DateTime.UtcNow.AddMinutes(15));

    public static IAccessTokenService Tokens()
    {
        var tokens = Substitute.For<IAccessTokenService>();
        tokens.UserTokenLifetime.Returns(TimeSpan.FromMinutes(15));
        tokens.IssueUserToken(Arg.Any<UserTokenRequest>()).Returns(_ => Token());
        tokens.IssueClientToken(Arg.Any<ClientTokenRequest>()).Returns(_ => Token("client-jwt", "jti-c"));
        return tokens;
    }
}
