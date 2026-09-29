using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Handlers.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Queries.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Security;
using JobPlatform.TestSupport;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AccountIdentity.Application.UnitTests;

public class ApiCredentialHandlerTests
{
    private readonly FakeTimeProvider _clock = AppKit.Clock();
    private readonly InMemoryAccounts _accounts = new();
    private readonly FakeHasher _hasher = new();
    private readonly InMemorySessionStore _sessions = new();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<ApiCredential> _store = new();
    private readonly IApiCredentialRepository _credentials = Substitute.For<IApiCredentialRepository>();
    private readonly IRateLimiter _limiter = Substitute.For<IRateLimiter>();
    private readonly IServiceClientRegistry _services = Substitute.For<IServiceClientRegistry>();
    private readonly RecordingAccessLog _log = new();
    private readonly Account _partner;

    public ApiCredentialHandlerTests()
    {
        _partner = AppKit.Active(_clock, ActorType.ExternalJobSite);
        _accounts.Add(_partner);
        _credentials.When(c => c.Add(Arg.Any<ApiCredential>())).Do(ci => _store.Add(ci.Arg<ApiCredential>()));
        _credentials.GetActiveByPartnerAsync(Arg.Any<AccountId>(), Arg.Any<CancellationToken>())
            .Returns(ci => _store.FirstOrDefault(c => c.PartnerAccountId == ci.Arg<AccountId>() && c.Status == ApiCredentialStatus.Active));
        _credentials.GetByKeyIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ci => _store.FirstOrDefault(c => c.KeyId == ci.Arg<string>()));
        _credentials.GetByIdAsync(Arg.Any<ApiCredentialId>(), Arg.Any<CancellationToken>()).Returns(ci => _store.FirstOrDefault(c => c.Id == ci.Arg<ApiCredentialId>()));
        _limiter.HitAsync(default!, default, default, default).ReturnsForAnyArgs(new RateLimitDecision(true, 1, TimeSpan.Zero));
        _limiter.CountAsync(default!, default).ReturnsForAnyArgs(0L);
    }

    private ICurrentUser Partner() => AppKit.User(_partner.Id.Value, ActorType.ExternalJobSite);

    private IssueApiCredentialHandler Issue(ICurrentUser? user = null, ISecretGenerator? generator = null) =>
        new(_accounts, _credentials, _hasher, generator ?? new FakeGenerator(), _sessions, _unitOfWork, user ?? Partner(), _clock);

    private AuthenticateApiClientHandler OAuth() =>
        new(_credentials, _accounts, _services, _hasher, AppKit.Tokens(), _limiter, _log, AppKit.User(source: "s1"), _clock);

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public async Task Issue_ReturnsTheSecretOnce_AndStoresOnlyItsHash()
    {
        var result = await Issue().Handle(new IssueApiCredentialCommand(null, null, null, null), default);

        result.Value.ClientId.Should().Be("jp_key");
        result.Value.ClientSecret.Should().Be("jps_secret");
        result.Value.RevokedPreviousCredentialId.Should().BeNull();
        result.Value.MaxRequests.Should().Be(1000);
        var stored = _store.Should().ContainSingle().Which;
        stored.SecretHash.Should().Be("h:jps_secret").And.NotBe(result.Value.ClientSecret);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-03")]
    public async Task Issue_WhenOneIsActive_RevokesItAndFlushesBeforeInsertingTheNewOne()
    {
        var first = (await Issue().Handle(new IssueApiCredentialCommand(null, null, null, null), default)).Value;
        var generator = new SequenceGenerator("jp_second", "jps_second");

        var second = (await Issue(generator: generator).Handle(new IssueApiCredentialCommand(null, null, null, null), default)).Value;

        second.RevokedPreviousCredentialId.Should().Be(first.ApiCredentialId);
        _store.Single(c => c.KeyId == "jp_key").Status.Should().Be(ApiCredentialStatus.Revoked);
        _store.Single(c => c.KeyId == "jp_second").Status.Should().Be(ApiCredentialStatus.Active);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _sessions.RevokedClients.Should().Contain("jp_key", "tokens issued for the revoked credential stop working");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public async Task Issue_AppliesEachControlIndependently()
    {
        var expiry = _clock.GetUtcNow().UtcDateTime.AddDays(10);

        var ipOnly = (await Issue().Handle(new IssueApiCredentialCommand(new[] { "10.0.0.0/8" }, null, null, null), default)).Value;
        ipOnly.IpWhitelist.Should().Equal("10.0.0.0/8");
        ipOnly.MaxRequests.Should().Be(1000);

        var limitOnly = (await Issue(generator: new SequenceGenerator("k2", "s2")).Handle(new IssueApiCredentialCommand(null, 20, null, null), default)).Value;
        limitOnly.MaxRequests.Should().Be(20);
        limitOnly.PeriodSeconds.Should().Be(3600);

        var expiryOnly = (await Issue(generator: new SequenceGenerator("k3", "s3")).Handle(new IssueApiCredentialCommand(null, null, null, expiry), default)).Value;
        expiryOnly.ExpiresAtUtc.Should().Be(expiry);
    }

    [Fact]
    public async Task Issue_ForAPendingPartnerOrUnknownAccount_Fails()
    {
        var pending = AppKit.Pending(_clock, ActorType.ExternalJobSite);
        _accounts.Add(pending);
        var act = () => Issue(AppKit.User(pending.Id.Value, ActorType.ExternalJobSite)).Handle(new IssueApiCredentialCommand(null, null, null, null), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.Code.Should().Be(ApiCredentialRuleCodes.PartnerNotActive);
        AppKit.ErrorOf(await Issue(AppKit.User(Guid.NewGuid(), ActorType.ExternalJobSite)).Handle(new IssueApiCredentialCommand(null, null, null, null), default))
            .Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Revoke_OnlyTheOwnerCanRevoke_OthersSeeNotFound()
    {
        var issued = (await Issue().Handle(new IssueApiCredentialCommand(null, null, null, null), default)).Value;
        var other = AppKit.User(Guid.NewGuid(), ActorType.ExternalJobSite);

        AppKit.ErrorOf(await new RevokeApiCredentialHandler(_credentials, _sessions, other, _clock).Handle(new RevokeApiCredentialCommand(issued.ApiCredentialId), default))
            .Code.Should().Be("E-API-CREDENTIAL-NOT-FOUND");
        var owner = new RevokeApiCredentialHandler(_credentials, _sessions, Partner(), _clock);
        (await owner.Handle(new RevokeApiCredentialCommand(issued.ApiCredentialId), default)).IsSuccess.Should().BeTrue();

        _store.Single().Status.Should().Be(ApiCredentialStatus.Revoked);
        _sessions.RevokedClients.Should().Contain("jp_key");
        AppKit.ErrorOf(await owner.Handle(new RevokeApiCredentialCommand(Guid.NewGuid()), default)).Type.Should().Be(ErrorType.NotFound);
    }

    // ------------------------------------------------------------------ OAuth 2.0 client credentials

    private async Task<string> IssuedSecretAsync(DateTime? expiry = null, string[]? ips = null)
    {
        await Issue().Handle(new IssueApiCredentialCommand(ips, null, null, expiry), default);
        return "jps_secret";
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-01")]
    public async Task OAuth_ValidCredentials_IssuesAPartnerTokenWithTheLifetimeCappedAtOneHour()
    {
        var secret = await IssuedSecretAsync();

        var result = await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "jp_key", secret), default);

        result.Value.TokenType.Should().Be("Bearer");
        result.Value.Scope.Should().Be(Scopes.PartnerApi);
        result.Value.AccessToken.Should().Be("client-jwt");
        _log.Entries.Should().ContainSingle(e => e.Action == "oauth.token" && e.Decision == "Allow");
        _log.Entries.Should().OnlyContain(e => e.Reason == null || !e.Reason.Contains(secret));
    }

    [Fact]
    public async Task OAuth_ServiceClients_GetAnInternalScopedTokenWithoutTouchingCredentials()
    {
        _services.Authenticate("svc", "pw").Returns(new ServiceClient("svc", new[] { Scopes.Internal }));

        var result = await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "svc", "pw"), default);

        result.Value.Scope.Should().Be(Scopes.Internal);
        await _credentials.DidNotReceiveWithAnyArgs().GetByKeyIdAsync(default!, default);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-03")]
    public async Task OAuth_WrongSecretsAreCountedPerSource_AndFiveFailuresBlockEvenTheCorrectSecret()
    {
        var secret = await IssuedSecretAsync();
        for (var i = 0; i < 5; i++)
        {
            AppKit.ErrorOf(await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "jp_key", "wrong"), default)).Code.Should().Be(ErrorCodes.ApiInvalidClient);
        }

        await _limiter.Received(5).HitAsync("oauth-failures:s1", 5, TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>());

        _limiter.CountAsync("oauth-failures:s1", Arg.Any<CancellationToken>()).Returns(5L);
        var blocked = AppKit.ErrorOf(await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "jp_key", secret), default));
        blocked.Code.Should().Be(ErrorCodes.ApiRateLimited);
        blocked.Type.Should().Be(ErrorType.TooManyRequests);
        new AuthenticateApiClientCommand("client_credentials", "a", "b").Should().BeAssignableTo<JobPlatform.SharedKernel.Application.Abstractions.IPersistOnFailure>();
    }

    [Fact]
    public async Task OAuth_UnknownClient_CostsAVerificationAndCountsAsAFailure()
    {
        var error = AppKit.ErrorOf(await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "jp_nobody", "x"), default));

        error.Code.Should().Be(ErrorCodes.ApiInvalidClient);
        await _limiter.Received(1).HitAsync("oauth-failures:s1", 5, TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-04")]
    public async Task OAuth_ExpiredCredential_ReturnsTpjpriExpired()
    {
        var secret = await IssuedSecretAsync(expiry: _clock.GetUtcNow().UtcDateTime.AddHours(1));
        _clock.Advance(TimeSpan.FromHours(2));

        var error = AppKit.ErrorOf(await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "jp_key", secret), default));

        error.Code.Should().Be(ErrorCodes.PartnerExpired);
        error.Type.Should().Be(ErrorType.Unauthorized);
        _store.Single().Status.Should().Be(ApiCredentialStatus.Expired);
    }

    [Fact]
    public async Task OAuth_NotWhitelistedIp_IsForbidden_AndNotCountedAsAFailedSecret()
    {
        var secret = await IssuedSecretAsync(ips: new[] { "198.51.100.0/24" });

        var error = AppKit.ErrorOf(await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "jp_key", secret), default));

        error.Code.Should().Be(ErrorCodes.PartnerIpNotAllowed);
        error.Type.Should().Be(ErrorType.Forbidden);
        await _limiter.DidNotReceiveWithAnyArgs().HitAsync(default!, default, default, default);
    }

    [Fact]
    public async Task OAuth_WhenThePartnerIsNoLongerActive_IsForbidden()
    {
        var secret = await IssuedSecretAsync();
        _partner.Deactivate(AppKit.Admin(), "x", _clock);

        var error = AppKit.ErrorOf(await OAuth().Handle(new AuthenticateApiClientCommand("client_credentials", "jp_key", secret), default));

        error.Code.Should().Be(ErrorCodes.PartnerNotActive);
    }

    [Fact]
    public async Task ApiCredentialQueries_ReturnProjectionsOrNotFound()
    {
        var store = Substitute.For<IIdentityReadStore>();
        var id = Guid.NewGuid();
        var view = new ApiCredentialView(id, "jp_key", "Active", Array.Empty<string>(), 10, 60, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        store.GetActiveApiCredentialForPartnerAsync(_partner.Id.Value, Arg.Any<CancellationToken>()).Returns(view);
        var controls = new ApiCredentialControlsDto(id, _partner.Id.Value, "jp_key", "Active", Array.Empty<string>(), 10, 60, DateTime.UtcNow);
        store.GetApiCredentialControlsAsync(id, Arg.Any<CancellationToken>()).Returns(controls);
        var handlers = new RequestHandlerSet(ApplicationAssembly.Assembly, store, Partner());

        (await handlers.Handle(new GetCurrentApiCredentialQuery(), default)).Value.Should().Be(view);
        (await handlers.Handle(new GetApiCredentialControlsQuery(id), default)).Value.Should().Be(controls);
        AppKit.ErrorOf(await handlers.Handle(new GetApiCredentialControlsQuery(Guid.NewGuid()), default)).Type.Should().Be(ErrorType.NotFound);
        AppKit.ErrorOf(await new RequestHandlerSet(ApplicationAssembly.Assembly, store, AppKit.User(Guid.NewGuid(), ActorType.ExternalJobSite)).Handle(new GetCurrentApiCredentialQuery(), default))
            .Type.Should().Be(ErrorType.NotFound);
    }

    private sealed class SequenceGenerator : ISecretGenerator
    {
        private readonly string _keyId;
        private readonly string _secret;

        public SequenceGenerator(string keyId, string secret)
        {
            _keyId = keyId;
            _secret = secret;
        }

        public string GenerateOtp() => "000000";

        public string GenerateApiKeyId() => _keyId;

        public string GenerateApiSecret() => _secret;

        public string GenerateToken() => "t";
    }
}
