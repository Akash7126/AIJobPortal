using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class ProfileRepository(JobSeekerProfileDbContext db) : IProfileRepository
{
    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Full(db).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Profile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        Full(db).FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public Task<bool> ExistsByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.Profiles.AnyAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public void Add(Profile profile) => db.Profiles.Add(profile);

    private static IQueryable<Profile> Full(JobSeekerProfileDbContext db) => db.Profiles
        .Include(p => p.Education).Include(p => p.Experience).Include(p => p.Skills).Include(p => p.Training).Include(p => p.Certificates)
        .Include(p => p.SocialLinks);
}
