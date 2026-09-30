using JobPlatform.JobSeekerProfile.Application.Commands.Privacy;
using JobPlatform.JobSeekerProfile.Application.Interfaces;
using JobPlatform.JobSeekerProfile.Application.Privacy;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Privacy;

internal sealed class RequestAccountDeactivationHandler(IProfileRepository profiles, IPrivacySettingRepository settings, IAccountIdentityClient identity,
    ICurrentUser user, TimeProvider clock) : ICommandHandler<RequestAccountDeactivationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RequestAccountDeactivationCommand request, CancellationToken ct)
    {
        var loaded = await PrivacySupport.LoadAsync(profiles, settings, user, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.Setting.RequestDeactivation(clock.GetUtcNow().UtcDateTime);
        if (loaded.Value.IsNew)
        {
            settings.Add(loaded.Value.Setting);
        }

        await identity.RequestDeactivationAsync(loaded.Value.Profile.OwnerAccountId, request.Reason ?? "JobSeekerRequested", "Deactivated", ct);
        return Result.Success();
    }
}
