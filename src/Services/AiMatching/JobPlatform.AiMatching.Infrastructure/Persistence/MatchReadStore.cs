using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Application.DTOs.Configuration;
using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.AiMatching.Application.DTOs.Parsing;
using JobPlatform.AiMatching.Application.DTOs.Recommendations;
using JobPlatform.AiMatching.Application.DTOs.Shortlists;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

/// <summary>Read side: AsNoTracking projections over stored scores and replicas. Never loads aggregates for display (foundation section 3.5).</summary>
internal sealed class MatchReadStore(AiMatchingDbContext db) : IMatchReadStore
{
    public async Task<PagedResult<MatchedJobDto>> ListJobRankingAsync(Guid profileId, decimal minScore, PageRequest page, CancellationToken ct = default)
    {
        var query = from s in db.MatchScores.AsNoTracking()
                    join p in db.KnownPostings.AsNoTracking() on s.JobPostingId equals p.Id
                    where s.ProfileId == profileId && s.Score >= minScore && !p.Suspended && p.Status.ToLower() == "active"
                    select new { s.Id, s.JobPostingId, p.Title, s.Score, s.ComputedAtUtc };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.Score).ThenBy(r => r.JobPostingId).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<MatchedJobDto>(rows.Select(r => new MatchedJobDto(r.Id, r.JobPostingId, r.Title, r.Score, r.ComputedAtUtc)).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<PagedResult<MatchedCandidateDto>> ListCandidatesAsync(Guid jobPostingId, decimal minScore, PageRequest page, CancellationToken ct = default)
    {
        var query = from s in db.MatchScores.AsNoTracking()
                    join p in db.KnownProfiles.AsNoTracking() on s.ProfileId equals p.Id
                    where s.JobPostingId == jobPostingId && s.Score >= minScore && p.Standing == KnownStanding.Active
                    select new { s.Id, s.ProfileId, s.Score, s.ComputedAtUtc };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.Score).ThenBy(r => r.ProfileId).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<MatchedCandidateDto>(rows.Select(r => new MatchedCandidateDto(r.Id, r.ProfileId, r.Score, r.ComputedAtUtc)).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<PagedResult<MatchScoreDto>> ListScoresWithBreakdownAsync(Guid jobPostingId, decimal minScore, PageRequest page, CancellationToken ct = default)
    {
        var query = db.MatchScores.AsNoTracking().Where(s => s.JobPostingId == jobPostingId && s.Score >= minScore);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(s => s.Score).ThenBy(s => s.ProfileId).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<MatchScoreDto>(rows.Select(s => new MatchScoreDto(s.Id, s.ProfileId, s.JobPostingId, s.Score, s.ConfigVersion.ToString(), s.ComputedAtUtc,
            s.Breakdown.Select(b => new MatchCriterionDto(b.Criterion.ToString(), b.Score, b.Weight, b.Included)).ToArray())).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<ParsedProfileDataDto?> GetParsedProfileDataAsync(Guid ownerAccountId, CancellationToken ct = default)
    {
        var data = await db.ParsedProfileData.AsNoTracking().Include(p => p.Fields).FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);
        return data is null
            ? null
            : new ParsedProfileDataDto(data.Id, data.ResumeId, data.ProfileId, data.Status.ToString(), MatchingConfiguration.DefaultLowConfidence,
                data.Fields.OrderBy(f => f.Name).Select(f => new ParsedProfileFieldDto(f.Name.ToString(), f.Value, f.Source.ToString(), f.Confidence, f.NeedsReview)).ToArray());
    }

    public async Task<ResumeParsedDataDto?> GetResumeParsedDataAsync(Guid id, CancellationToken ct = default)
    {
        var data = await db.ResumeParsedData.AsNoTracking().Include(r => r.Fields).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (data is null)
        {
            return null;
        }

        var run = await db.SkillStandardizations.AsNoTracking().Include(s => s.Mappings).FirstOrDefaultAsync(s => s.ResumeParsedDataId == id, ct);
        return new ResumeParsedDataDto(data.Id, data.ResumeId, data.ProfileId, data.Language.ToString(), data.Status.ToString(), data.FailureCode,
            data.Fields.OrderBy(f => f.Name).Select(f => new ParsedFieldDto(f.Name.ToString(), f.Value, f.Confidence, f.NeedsReview)).ToArray(),
            (run?.Mappings ?? Array.Empty<SkillMapping>()).Select(m => new SkillMappingDto(m.ExtractedTerm, m.CanonicalCode, m.Confidence, m.IsFreeText, m.NeedsReview)).ToArray());
    }

    public async Task<ShortlistDto?> GetShortlistAsync(Guid id, CancellationToken ct = default)
    {
        var s = await db.CandidateShortlists.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null
            ? null
            : new ShortlistDto(s.Id, s.JobPostingId, s.EmployerAccountId, s.Status.ToString(), s.RequestedSize, s.ConfigVersion,
                s.Items.Select(i => new ShortlistEntryDto(i.Rank, i.ProfileId, i.Score)).ToArray(), s.FailureReason, s.RequestedAtUtc, s.ComputedAtUtc);
    }

    public async Task<JobRecommendationDto?> GetLatestRecommendationAsync(Guid profileId, CancellationToken ct = default)
    {
        var latest = await db.JobRecommendations.AsNoTracking().Where(r => r.ProfileId == profileId).OrderByDescending(r => r.ComputedAtUtc).FirstOrDefaultAsync(ct);
        if (latest is null)
        {
            return null;
        }

        var ids = latest.Items.Select(i => i.JobPostingId).ToArray();
        var titles = await db.KnownPostings.AsNoTracking().Where(p => ids.Contains(p.Id) && !p.Suspended && p.Status.ToLower() == "active").ToDictionaryAsync(p => p.Id, p => p.Title, ct);
        // Postings closed since the list was computed are dropped from the answer.
        return new JobRecommendationDto(latest.Id, latest.Strategy.ToString(), latest.ComputedAtUtc,
            latest.Items.Where(i => titles.ContainsKey(i.JobPostingId)).Select(i => new RecommendedJobDto(i.JobPostingId, titles[i.JobPostingId], i.Score, i.Reason.ToString())).ToArray());
    }

    public async Task<MatchingConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default)
    {
        var c = await db.MatchingConfigurations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == MatchingConfiguration.SingletonId, ct);
        return c is null
            ? null
            : new MatchingConfigurationDto(c.ConfigVersion, c.MatchThresholdPercent,
                new WeightsDto(c.Weights.SkillOverlap, c.Weights.Education, c.Weights.Training, c.Weights.Location, c.Weights.Experience, c.Weights.Salary),
                c.ShortlistSize, c.LowConfidenceThresholdPercent, c.UpdatedAtUtc, ETag.From(c.RowVersion));
    }
}
