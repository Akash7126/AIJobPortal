using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.ApiCredentials;

// ---------------------------------------------------------------------- issue / revoke (US-3.1.3-02)

/// <summary>IP whitelist, usage limit and expiration are independently optional; unset ones take platform defaults.</summary>
public sealed record IssueApiCredentialCommand(IReadOnlyList<string>? IpWhitelist, int? MaxRequests, int? PeriodSeconds, DateTime? ExpiresAtUtc)
    : ICommand<IssuedApiCredentialDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };

    public string? RequiredPermission => Permissions.ApiCredentialsManage;
}

/// <param name="ClientSecret">Shown exactly once, in this response. Only its hash is stored and it never appears in events or logs.</param>
public sealed record IssuedApiCredentialDto(Guid ApiCredentialId, string ClientId, string ClientSecret, DateTime ExpiresAtUtc, int MaxRequests,
    int PeriodSeconds, IReadOnlyList<string> IpWhitelist, Guid? RevokedPreviousCredentialId);

public sealed record RevokeApiCredentialCommand(Guid ApiCredentialId) : ICommand<Unit>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };

    public string? RequiredPermission => Permissions.ApiCredentialsManage;
}

public sealed class IssueApiCredentialValidator : AbstractValidator<IssueApiCredentialCommand>
{
    public IssueApiCredentialValidator(TimeProvider clock)
    {
        When(x => x.IpWhitelist is { Count: > 0 }, () =>
            RuleForEach(x => x.IpWhitelist!).Must(BeIpOrCidr).WithErrorCode("VAL.IpWhitelist.Invalid"));
        When(x => x.MaxRequests.HasValue, () =>
            RuleFor(x => x.MaxRequests!.Value).GreaterThan(0).OverridePropertyName("MaxRequests").WithErrorCode("VAL.MaxRequests.Invalid"));
        When(x => x.PeriodSeconds.HasValue, () =>
            RuleFor(x => x.PeriodSeconds!.Value).GreaterThan(0).OverridePropertyName("PeriodSeconds").WithErrorCode("VAL.PeriodSeconds.Invalid"));
        When(x => x.ExpiresAtUtc.HasValue, () =>
            RuleFor(x => x.ExpiresAtUtc!.Value).Must(e =>
                    e > clock.GetUtcNow().UtcDateTime && e <= clock.GetUtcNow().UtcDateTime.AddDays(AccountDefaults.MaxCredentialLifetimeDays))
                .OverridePropertyName("ExpiresAtUtc").WithErrorCode("VAL.ExpiresAtUtc.OutOfRange"));
    }

    private static bool BeIpOrCidr(string? value) =>
        !string.IsNullOrWhiteSpace(value) && (value.Contains('/') ? System.Net.IPNetwork.TryParse(value, out _) : System.Net.IPAddress.TryParse(value, out _));
}

public sealed class RevokeApiCredentialValidator : AbstractValidator<RevokeApiCredentialCommand>
{
    public RevokeApiCredentialValidator() => RuleFor(x => x.ApiCredentialId).NotEmpty().WithErrorCode("VAL.ApiCredentialId.Required");
}

internal sealed class IssueApiCredentialHandler : ICommandHandler<IssueApiCredentialCommand, IssuedApiCredentialDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IApiCredentialRepository _credentials;
    private readonly IApiSecretHasher _secretHasher;
    private readonly ISecretGenerator _generator;
    private readonly ISessionStore _sessions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public IssueApiCredentialHandler(IAccountRepository accounts, IApiCredentialRepository credentials, IApiSecretHasher secretHasher,
        ISecretGenerator generator, ISessionStore sessions, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    {
        _accounts = accounts;
        _credentials = credentials;
        _secretHasher = secretHasher;
        _generator = generator;
        _sessions = sessions;
        _unitOfWork = unitOfWork;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<IssuedApiCredentialDto>> Handle(IssueApiCredentialCommand request, CancellationToken ct)
    {
        var partnerId = new AccountId(_user.UserId!.Value);
        var partner = await _accounts.GetByIdAsync(partnerId, ct);
        if (partner is null)
        {
            return AccountErrors.NotFound;
        }

        // D-02: a new request revokes the previous credential first (one active credential per partner, INV-13).
        var existing = await _credentials.GetActiveByPartnerAsync(partnerId, ct);
        Guid? revokedId = null;
        if (existing is not null)
        {
            existing.Revoke(partnerId.Value, _clock);
            revokedId = existing.Id.Value;
            await _sessions.RevokeClientAsync(existing.KeyId, TimeSpan.FromHours(1), ct);
            // Flushed inside the command's transaction so the filtered unique index never sees two active rows.
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var secret = _generator.GenerateApiSecret();
        var limits = request.MaxRequests.HasValue || request.PeriodSeconds.HasValue
            ? new UsageLimits(request.MaxRequests ?? AccountDefaults.DefaultCredentialMaxRequests, request.PeriodSeconds ?? AccountDefaults.DefaultCredentialPeriodSeconds)
            : null;
        var credential = ApiCredential.Issue(partner, _generator.GenerateApiKeyId(), _secretHasher.Hash(secret),
            new CredentialControls(request.IpWhitelist, limits, request.ExpiresAtUtc), existing, partnerId.Value, _clock);
        _credentials.Add(credential);

        return new IssuedApiCredentialDto(credential.Id.Value, credential.KeyId, secret, credential.ExpiresAtUtc, credential.Limits.MaxRequests,
            credential.Limits.PeriodSeconds, credential.IpWhitelist.Entries, revokedId);
    }
}

internal sealed class RevokeApiCredentialHandler : ICommandHandler<RevokeApiCredentialCommand, Unit>
{
    private static readonly Error NotFound = Error.NotFound("E-API-CREDENTIAL-NOT-FOUND", "The API credential was not found.");

    private readonly IApiCredentialRepository _credentials;
    private readonly ISessionStore _sessions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RevokeApiCredentialHandler(IApiCredentialRepository credentials, ISessionStore sessions, ICurrentUser user, TimeProvider clock)
    {
        _credentials = credentials;
        _sessions = sessions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RevokeApiCredentialCommand request, CancellationToken ct)
    {
        var credential = await _credentials.GetByIdAsync(new ApiCredentialId(request.ApiCredentialId), ct);

        // A partner may only revoke its own credential; anything else looks like "not found" so ids cannot be probed.
        if (credential is null || credential.PartnerAccountId.Value != _user.UserId)
        {
            return NotFound;
        }

        credential.Revoke(_user.UserId!.Value, _clock);
        await _sessions.RevokeClientAsync(credential.KeyId, TimeSpan.FromHours(1), ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- OAuth 2.0 client credentials (US-3.4.3-04)

public sealed record ClientTokenDto(string AccessToken, string TokenType, int ExpiresIn, string Scope);

/// <summary>Failed attempts are counted (aggregate and source), so state is persisted on failure.</summary>
public sealed record AuthenticateApiClientCommand(string GrantType, string ClientId, string ClientSecret)
    : ICommand<ClientTokenDto>, IPersistOnFailure;

public sealed class AuthenticateApiClientValidator : AbstractValidator<AuthenticateApiClientCommand>
{
    public AuthenticateApiClientValidator()
    {
        RuleFor(x => x.GrantType).Equal("client_credentials").WithErrorCode("VAL.GrantType.Unsupported");
        RuleFor(x => x.ClientId).NotEmpty().WithErrorCode("VAL.ClientId.Required").MaximumLength(128).WithErrorCode("VAL.ClientId.TooLong");
        RuleFor(x => x.ClientSecret).NotEmpty().WithErrorCode("VAL.ClientSecret.Required").MaximumLength(256).WithErrorCode("VAL.ClientSecret.TooLong");
    }
}

internal sealed class AuthenticateApiClientHandler : ICommandHandler<AuthenticateApiClientCommand, ClientTokenDto>
{
    private static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromHours(1);
    private static readonly Error InvalidClient = Error.Unauthorized(ErrorCodes.ApiInvalidClient, "The client credentials are invalid.");

    private readonly IApiCredentialRepository _credentials;
    private readonly IAccountRepository _accounts;
    private readonly IServiceClientRegistry _serviceClients;
    private readonly IApiSecretHasher _secretHasher;
    private readonly IAccessTokenService _tokens;
    private readonly IRateLimiter _limiter;
    private readonly IAccessLog _accessLog;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AuthenticateApiClientHandler(IApiCredentialRepository credentials, IAccountRepository accounts, IServiceClientRegistry serviceClients,
        IApiSecretHasher secretHasher, IAccessTokenService tokens, IRateLimiter limiter, IAccessLog accessLog, ICurrentUser user, TimeProvider clock)
    {
        _credentials = credentials;
        _accounts = accounts;
        _serviceClients = serviceClients;
        _secretHasher = secretHasher;
        _tokens = tokens;
        _limiter = limiter;
        _accessLog = accessLog;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<ClientTokenDto>> Handle(AuthenticateApiClientCommand request, CancellationToken ct)
    {
        var failureKey = $"oauth-failures:{_user.SourceKey}";
        if (await _limiter.CountAsync(failureKey, ct) >= AccountDefaults.MaxCredentialFailedAttempts)
        {
            return Error.TooManyRequests(ErrorCodes.ApiRateLimited, "Too many failed authentication attempts. Try again later.", TimeSpan.FromMinutes(15));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (_serviceClients.Authenticate(request.ClientId, request.ClientSecret) is { } service)
        {
            var token = _tokens.IssueClientToken(new ClientTokenRequest(Guid.Empty, service.ClientId, service.Scopes, Array.Empty<Guid>(), ActorType.System, MaxTokenLifetime));
            await LogAsync(null, "Allow", "service-client", ct);
            return ToDto(token, service.Scopes, now);
        }

        var credential = await _credentials.GetByKeyIdAsync(request.ClientId, ct);
        if (credential is null)
        {
            _secretHasher.Verify(request.ClientSecret, "0000000000000000000000000000000000000000000000000000000000000000");
            await _limiter.HitAsync(failureKey, AccountDefaults.MaxCredentialFailedAttempts, AccountDefaults.FailedLoginWindow, ct);
            await LogAsync(null, "Deny", "unknown-client", ct);
            return InvalidClient;
        }

        try
        {
            credential.Authenticate(request.ClientSecret, _user.IpAddress, _secretHasher, _clock);
        }
        catch (BusinessRuleViolationException ex)
        {
            if (ex.Code == ApiCredentialRuleCodes.InvalidSecret)
            {
                await _limiter.HitAsync(failureKey, AccountDefaults.MaxCredentialFailedAttempts, AccountDefaults.FailedLoginWindow, ct);
            }

            await LogAsync(credential.PartnerAccountId.Value, "Deny", ex.Code, ct);
            return ex.ToError();
        }

        var partner = await _accounts.GetByIdAsync(credential.PartnerAccountId, ct);
        if (partner is null || partner.Standing != AccountStanding.Active)
        {
            await LogAsync(credential.PartnerAccountId.Value, "Deny", "partner-not-active", ct);
            return Error.Forbidden(ErrorCodes.PartnerNotActive, "The partner account is not active.");
        }

        var lifetime = TimeSpan.FromTicks(Math.Min(MaxTokenLifetime.Ticks, (credential.ExpiresAtUtc - now).Ticks));
        var scopes = new[] { Scopes.PartnerApi };
        var issued = _tokens.IssueClientToken(new ClientTokenRequest(partner.Id.Value, credential.KeyId, scopes,
            partner.RoleAssignments.Select(r => r.RoleId.Value).ToArray(), ActorType.ExternalJobSite, lifetime));
        await LogAsync(partner.Id.Value, "Allow", "client-credentials", ct);
        return ToDto(issued, scopes, now);
    }

    private static ClientTokenDto ToDto(IssuedAccessToken token, IReadOnlyCollection<string> scopes, DateTime now) =>
        new(token.Token, "Bearer", (int)Math.Max(0, (token.ExpiresAtUtc - now).TotalSeconds), string.Join(' ', scopes));

    private Task LogAsync(Guid? accountId, string decision, string? reason, CancellationToken ct) =>
        _accessLog.AppendAsync(new AccessLogEntry(_clock.GetUtcNow().UtcDateTime, accountId, "oauth.token", "oauth", decision, reason, _user.IpAddress), ct);
}

// ---------------------------------------------------------------------- queries

public sealed record GetCurrentApiCredentialQuery : IQuery<ApiCredentialView>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.ExternalJobSite };
}

public sealed record GetApiCredentialControlsQuery(Guid ApiCredentialId) : ServiceAuthorized, IQuery<ApiCredentialControlsDto>;

internal sealed class ApiCredentialQueryHandlers :
    IQueryHandler<GetCurrentApiCredentialQuery, ApiCredentialView>,
    IQueryHandler<GetApiCredentialControlsQuery, ApiCredentialControlsDto>
{
    private readonly IIdentityReadStore _store;
    private readonly ICurrentUser _user;

    public ApiCredentialQueryHandlers(IIdentityReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<ApiCredentialView>> Handle(GetCurrentApiCredentialQuery request, CancellationToken ct)
    {
        var view = await _store.GetActiveApiCredentialForPartnerAsync(_user.UserId!.Value, ct);
        return view is null ? Error.NotFound("E-API-CREDENTIAL-NOT-FOUND", "The partner has no active API credential.") : view;
    }

    public async Task<Result<ApiCredentialControlsDto>> Handle(GetApiCredentialControlsQuery request, CancellationToken ct)
    {
        var view = await _store.GetApiCredentialControlsAsync(request.ApiCredentialId, ct);
        return view is null ? Error.NotFound("E-API-CREDENTIAL-NOT-FOUND", "The API credential was not found.") : view;
    }
}
