using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;
using JobPlatform.CandidateSourcing.Application.Interfaces;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence;

/// <summary>Read side (foundation section 3.5): AsNoTracking projections over the local replica, never aggregates.</summary>
internal sealed class CandidateSourcingReadStore(CandidateSourcingDbContext db) : ICandidateSourcingReadStore
{
    public async Task<IReadOnlyList<TalentPoolEntryView>> ListTalentPoolAsync(Guid employerAccountId, CancellationToken ct = default) =>
        (await db.TalentPoolEntries.AsNoTracking().Where(e => e.EmployerAccountId == employerAccountId && !e.Removed)
            .OrderByDescending(e => e.AddedAtUtc).ToListAsync(ct))
        .Select(e => new TalentPoolEntryView(e.Id, e.CandidateProfileId, e.JobPostingId, e.Note, e.AddedAtUtc)).ToList();

    public async Task<PagedResult<CandidateSearchResultItemView>> SearchCandidatesAsync(CandidateSearchCriteria criteria, PageRequest page,
        CancellationToken ct = default)
    {
        var query = db.CandidateProjections.AsNoTracking()
            .Where(p => !p.Deactivated && (p.Visibility == Domain.Privacy.CandidateVisibility.Public || p.EmployerVisibilityOptIn));

        if (criteria.EducationLevel is { Length: > 0 })
        {
            query = query.Where(p => p.EducationLevel == criteria.EducationLevel);
        }

        if (criteria.LocationCode is { Length: > 0 })
        {
            query = query.Where(p => p.LocationCode == criteria.LocationCode);
        }

        if (criteria.MinExperienceYears is { } min)
        {
            query = query.Where(p => p.YearsOfExperience >= min);
        }

        if (criteria.MaxExperienceYears is { } max)
        {
            query = query.Where(p => p.YearsOfExperience <= max);
        }

        if (criteria.SalaryMin is { } salaryMin)
        {
            query = query.Where(p => p.SalaryMax == null || p.SalaryMax >= salaryMin);
        }

        if (criteria.SalaryMax is { } salaryMax)
        {
            query = query.Where(p => p.SalaryMin == null || p.SalaryMin <= salaryMax);
        }

        if (criteria.Availability is { Length: > 0 })
        {
            query = query.Where(p => p.Availability == criteria.Availability);
        }

        var candidates = await query.OrderBy(p => p.ProfileId).ToListAsync(ct);
        if (criteria.Skills is { Count: > 0 } skills)
        {
            candidates = candidates.Where(p => skills.All(s => p.Skills.Contains(s, StringComparer.OrdinalIgnoreCase))).ToList();
        }

        var total = candidates.Count;
        var items = candidates.Skip(page.Skip).Take(page.PageSize)
            .Select(p => new CandidateSearchResultItemView(p.ProfileId, p.Skills, p.EducationLevel, p.YearsOfExperience, p.LocationCode, p.SalaryMin, p.SalaryMax))
            .ToList();
        return new PagedResult<CandidateSearchResultItemView>(items, page.Page, page.PageSize, total);
    }
}
