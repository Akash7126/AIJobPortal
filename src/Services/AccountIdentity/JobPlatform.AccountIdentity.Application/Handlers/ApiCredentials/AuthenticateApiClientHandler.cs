using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Handlers.ApiCredentials;

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
