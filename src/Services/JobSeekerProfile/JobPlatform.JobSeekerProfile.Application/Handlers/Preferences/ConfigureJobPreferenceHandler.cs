using JobPlatform.JobSeekerProfile.Application.Commands.Preferences;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Preferences;

internal sealed class ConfigureJobPreferenceHandler(IJobPreferenceRepository repository, IProfileRepository profiles, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ConfigureJobPreferenceCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ConfigureJobPreferenceCommand request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var preference = await repository.GetByProfileAsync(profile.Id, ct);
        var isNew = preference is null;
        preference ??= JobPreference.CreateEmpty(profile.Id);
        preference.EnsureOwnedBy(new Actor(user.UserId!.Value), profile.OwnerAccountId);
        preference.Set(request.JobTypes, request.Industries, request.Locations, new SalaryRange(request.SalaryMin, request.SalaryMax, request.SalaryCurrency),
            request.WorkArrangements.Select(w => Enum.Parse<WorkArrangement>(w, true)).ToList(), clock.GetUtcNow().UtcDateTime);
        if (isNew)
        {
            repository.Add(preference);
        }

        return Result.Success();
    }
}
