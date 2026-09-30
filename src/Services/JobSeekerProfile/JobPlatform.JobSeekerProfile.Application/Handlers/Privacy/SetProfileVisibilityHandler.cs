using JobPlatform.JobSeekerProfile.Application.Commands.Privacy;
using JobPlatform.JobSeekerProfile.Application.Privacy;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Privacy;

internal sealed class SetProfileVisibilityHandler(IProfileRepository profiles, IPrivacySettingRepository settings, ICurrentUser user)
    : ICommandHandler<SetProfileVisibilityCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetProfileVisibilityCommand request, CancellationToken ct)
    {
        var loaded = await PrivacySupport.LoadAsync(profiles, settings, user, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.Setting.SetVisibility(request.Public, request.PublicSharingActive);
        if (loaded.Value.IsNew)
        {
            settings.Add(loaded.Value.Setting);
        }

        return Result.Success();
    }
}
