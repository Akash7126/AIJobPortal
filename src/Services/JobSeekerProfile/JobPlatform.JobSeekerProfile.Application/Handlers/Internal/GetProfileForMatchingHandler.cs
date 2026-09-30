using JobPlatform.JobSeekerProfile.Application.Queries.Internal;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Internal;

internal sealed class GetProfileForMatchingHandler(IProfileRepository profiles) : IQueryHandler<GetProfileForMatchingQuery, ProfileForMatchingDto?>
{
    public async Task<Result<ProfileForMatchingDto?>> Handle(GetProfileForMatchingQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByIdAsync(request.ProfileId, ct);
        if (profile is null)
        {
            return Result.Success<ProfileForMatchingDto?>(null);
        }

        return new ProfileForMatchingDto(profile.Id, profile.OwnerAccountId, profile.Status.ToString(), profile.Version, null,
            profile.Skills.Select(s => s.Name).ToList(), profile.Education.OrderByDescending(e => e.To).FirstOrDefault()?.Degree,
            profile.Training.Select(t => t.Name).ToList(), profile.Address?.Governorate, profile.Address?.City, Array.Empty<string>(),
            profile.YearsOfExperience, profile.SalaryExpectation?.Min, profile.SalaryExpectation?.Max);
    }
}
