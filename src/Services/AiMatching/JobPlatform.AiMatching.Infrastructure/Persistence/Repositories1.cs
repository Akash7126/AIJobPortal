using JobPlatform.AiMatching.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

// Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).

internal sealed class MatchingConfigurationRepository(AiMatchingDbContext db) : IMatchingConfigurationRepository
{
    public Task<MatchingConfiguration?> GetCurrentAsync(CancellationToken ct = default) =>
        db.MatchingConfigurations.Include(c => c.History).FirstOrDefaultAsync(c => c.Id == MatchingConfiguration.SingletonId, ct);

    public void Add(MatchingConfiguration configuration) => db.MatchingConfigurations.Add(configuration);
}

internal sealed class MatchScoreRepository(AiMatchingDbContext db) : IMatchScoreRepository
{
    public Task<MatchScore?> GetPairAsync(Guid profileId, Guid jobPostingId, CancellationToken ct = default) =>
        db.MatchScores.FirstOrDefaultAsync(s => s.ProfileId == profileId && s.JobPostingId == jobPostingId, ct);

    public async Task<IReadOnlyList<MatchScore>> ListByPostingAsync(Guid jobPostingId, CancellationToken ct = default) =>
        await db.MatchScores.Where(s => s.JobPostingId == jobPostingId).OrderByDescending(s => s.Score).ThenBy(s => s.ProfileId).ToListAsync(ct);

    public async Task<IReadOnlyList<MatchScore>> ListByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        await db.MatchScores.Where(s => s.ProfileId == profileId).OrderByDescending(s => s.Score).ThenBy(s => s.JobPostingId).ToListAsync(ct);

    public async Task<int> RemoveByPostingAsync(Guid jobPostingId, CancellationToken ct = default)
    {
        var stored = await db.MatchScores.Where(s => s.JobPostingId == jobPostingId).ToListAsync(ct);
        db.MatchScores.RemoveRange(stored);
        return stored.Count;
    }

    public void Add(MatchScore score) => db.MatchScores.Add(score);
}

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

internal sealed class SkillStandardizationRepository(AiMatchingDbContext db) : ISkillStandardizationRepository
{
    public Task<SkillStandardization?> GetByResumeParsedDataAsync(Guid resumeParsedDataId, CancellationToken ct = default) =>
        db.SkillStandardizations.Include(s => s.Mappings).FirstOrDefaultAsync(s => s.ResumeParsedDataId == resumeParsedDataId, ct);

    public async Task<IReadOnlyList<SkillStandardization>> ListNotOnTaxonomyVersionAsync(string taxonomyVersion, int take, CancellationToken ct = default) =>
        await db.SkillStandardizations.Include(s => s.Mappings).Where(s => s.TaxonomyVersion != taxonomyVersion).OrderBy(s => s.UpdatedAtUtc).Take(take).ToListAsync(ct);

    public void Add(SkillStandardization standardization) => db.SkillStandardizations.Add(standardization);
}

internal sealed class ParsedProfileDataRepository(AiMatchingDbContext db) : IParsedProfileDataRepository
{
    public Task<ParsedProfileData?> GetByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.ParsedProfileData.Include(p => p.Fields).FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public Task<ParsedProfileData?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.ParsedProfileData.Include(p => p.Fields).FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public void Add(ParsedProfileData data) => db.ParsedProfileData.Add(data);
}

internal sealed class JobSemanticsRepository(AiMatchingDbContext db) : IJobSemanticsRepository
{
    public Task<JobSemantics?> GetAsync(Guid jobPostingId, CancellationToken ct = default) => db.JobSemantics.FirstOrDefaultAsync(s => s.Id == jobPostingId, ct);

    public void Add(JobSemantics semantics) => db.JobSemantics.Add(semantics);
}

internal sealed class JobRecommendationRepository(AiMatchingDbContext db) : IJobRecommendationRepository
{
    public Task<JobRecommendation?> GetLatestAsync(Guid profileId, CancellationToken ct = default) =>
        db.JobRecommendations.Where(r => r.ProfileId == profileId).OrderByDescending(r => r.ComputedAtUtc).FirstOrDefaultAsync(ct);

    public void Add(JobRecommendation recommendation) => db.JobRecommendations.Add(recommendation);
}
