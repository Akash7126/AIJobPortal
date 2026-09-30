using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using ProfileAggregate = JobPlatform.JobSeekerProfile.Domain.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Profile;

internal static class GenderValues
{
    public static readonly string[] Allowed = Enum.GetNames<Gender>();
}

/// <summary>Loads the caller's profile and enforces the optional If-Match precondition as a 409 E-JSRPM-CONFLICT (Decision D-01: reject, not merge).</summary>
internal static class ProfileCommandSupport
{
    public static async Task<Result<ProfileAggregate>> LoadOwnedAsync(IProfileRepository profiles, ICurrentUser user, string? ifMatch, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        if (ifMatch is not null && !ETag.Matches(ifMatch, profile.RowVersion))
        {
            return Error.Conflict(ErrorCodes.Conflict, "The profile was modified since it was last read. Reload and retry.");
        }

        return profile;
    }
}
