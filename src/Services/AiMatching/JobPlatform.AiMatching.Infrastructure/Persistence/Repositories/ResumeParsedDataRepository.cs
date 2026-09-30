using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class ResumeParsedDataRepository(AiMatchingDbContext db) : IResumeParsedDataRepository
{
    public Task<ResumeParsedData?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.ResumeParsedData.Include(r => r.Fields).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<ResumeParsedData?> GetCurrentByResumeAsync(Guid resumeId, CancellationToken ct = default) =>
        db.ResumeParsedData.Include(r => r.Fields).Where(r => r.ResumeId == resumeId && r.SupersededBy == null).OrderByDescending(r => r.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public Task<ResumeParsedData?> GetLatestByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.ResumeParsedData.Where(r => r.ProfileId == profileId && r.SupersededBy == null && r.Status == ParseStatus.Parsed)
            .OrderByDescending(r => r.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(Guid resumeId, string sha256, CancellationToken ct = default) =>
        db.ResumeParsedData.AnyAsync(r => r.ResumeId == resumeId && r.Sha256 == sha256, ct);

    public void Add(ResumeParsedData data) => db.ResumeParsedData.Add(data);
}
