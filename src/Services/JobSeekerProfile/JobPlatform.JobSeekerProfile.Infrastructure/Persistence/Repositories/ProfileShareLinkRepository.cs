using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class ProfileShareLinkRepository(JobSeekerProfileDbContext db) : IProfileShareLinkRepository
{
    public Task<ProfileShareLink?> GetActiveByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.ShareLinks.FirstOrDefaultAsync(l => l.ProfileId == profileId && l.IsActive, ct);

    public Task<ProfileShareLink?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        db.ShareLinks.FirstOrDefaultAsync(l => l.Token == token, ct);

    public void Add(ProfileShareLink link) => db.ShareLinks.Add(link);
}
