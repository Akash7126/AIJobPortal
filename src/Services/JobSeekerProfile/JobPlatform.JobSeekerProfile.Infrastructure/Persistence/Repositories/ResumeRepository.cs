using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class ResumeRepository(JobSeekerProfileDbContext db) : IResumeRepository
{
    public Task<Resume?> GetCurrentByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.Resumes.FirstOrDefaultAsync(r => r.ProfileId == profileId && r.IsCurrent, ct);

    public Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Resumes.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Add(Resume resume) => db.Resumes.Add(resume);
}
