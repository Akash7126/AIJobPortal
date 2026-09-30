using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class ProcessedParsedDataRepository(JobSeekerProfileDbContext db) : IProcessedParsedDataRepository
{
    public Task<bool> ExistsAsync(Guid resumeParsedDataId, CancellationToken ct = default) =>
        db.ProcessedParsedData.AnyAsync(p => p.ResumeParsedDataId == resumeParsedDataId, ct);

    public void Add(ProcessedParsedData record) => db.ProcessedParsedData.Add(record);
}
