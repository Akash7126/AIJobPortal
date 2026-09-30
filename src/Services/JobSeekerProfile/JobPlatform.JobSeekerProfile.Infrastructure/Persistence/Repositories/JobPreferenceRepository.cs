using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class JobPreferenceRepository(JobSeekerProfileDbContext db) : IJobPreferenceRepository
{
    public Task<JobPreference?> GetByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.JobPreferences.FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public void Add(JobPreference preference) => db.JobPreferences.Add(preference);
}
