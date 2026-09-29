using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.ApiCredentials;

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
