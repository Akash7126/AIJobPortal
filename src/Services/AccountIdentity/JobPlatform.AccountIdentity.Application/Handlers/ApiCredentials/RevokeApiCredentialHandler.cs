using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.ApiCredentials;

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
