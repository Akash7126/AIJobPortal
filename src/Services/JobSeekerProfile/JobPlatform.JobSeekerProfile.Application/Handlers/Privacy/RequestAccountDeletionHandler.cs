using JobPlatform.JobSeekerProfile.Application.Commands.Privacy;
using JobPlatform.JobSeekerProfile.Application.Privacy;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Privacy;

internal sealed class RequestAccountDeletionHandler(IProfileRepository profiles, IPrivacySettingRepository settings, IAccountIdentityClient identity,
    ICurrentUser user, TimeProvider clock) : ICommandHandler<RequestAccountDeletionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RequestAccountDeletionCommand request, CancellationToken ct)
    {
        var loaded = await PrivacySupport.LoadAsync(profiles, settings, user, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.Setting.RequestDeletion(clock.GetUtcNow().UtcDateTime);
        if (loaded.Value.IsNew)
        {
            settings.Add(loaded.Value.Setting);
        }

        await identity.RequestDeactivationAsync(loaded.Value.Profile.OwnerAccountId, "DeletionRequested", "DeletionRequested", ct);
        return Result.Success();
    }
}
