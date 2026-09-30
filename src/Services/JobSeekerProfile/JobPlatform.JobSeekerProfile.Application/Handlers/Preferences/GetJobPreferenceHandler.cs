using JobPlatform.JobSeekerProfile.Application.DTOs.Common;
using JobPlatform.JobSeekerProfile.Application.DTOs.Preferences;
using JobPlatform.JobSeekerProfile.Application.Queries.Preferences;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Preferences;

internal sealed class GetJobPreferenceHandler(IJobPreferenceRepository repository, IProfileRepository profiles, ICurrentUser user)
    : IQueryHandler<GetJobPreferenceQuery, JobPreferenceView>
{
    public async Task<Result<JobPreferenceView>> Handle(GetJobPreferenceQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var preference = await repository.GetByProfileAsync(profile.Id, ct);
        if (preference is null)
        {
            return new JobPreferenceView(profile.Id, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, Array.Empty<string>(),
                DateTime.MinValue);
        }

        return new JobPreferenceView(preference.ProfileId, preference.JobTypes, preference.Industries, preference.Locations,
            preference.SalaryExpectation is null ? null : new SalaryRangeView(preference.SalaryExpectation.Min, preference.SalaryExpectation.Max,
                preference.SalaryExpectation.Currency), preference.WorkArrangements.Select(w => w.ToString()).ToList(), preference.UpdatedAtUtc);
    }
}
