using JobPlatform.JobPosting.Application;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.Persistence;

/// <summary>
/// Read side (foundation section 3.5). Production SQL Server deployments should add a full-text index on Title/Summary (Arabic 1025 + English 1033,
/// handover section 3.5/T-04) and switch <see cref="Search"/>'s keyword predicate to <c>CONTAINS</c>; this implementation uses a portable
/// <c>LIKE</c> predicate that runs identically on SQL Server and SQLite (dev/tests) so the service works out of the box on both, at the cost of the
/// THR-013 (&lt;= 2s at 50k postings) target, which needs the real full-text index. See the BC-09 status doc.
/// </summary>
internal sealed class JobPostingSearchReadModel : IJobPostingSearchReadModel
{
    private readonly JobPostingDbContext _db;

    public JobPostingSearchReadModel(JobPostingDbContext db) => _db = db;

    public async Task<PagedResult<JobPostingSummaryView>> SearchAsync(SearchCriteriaInput criteria, string? sort, PageRequest page, CancellationToken ct = default)
    {
        var query = _db.JobPostings.AsNoTracking().Where(p => p.Status == JobPostingStatus.Active && p.Visibility.Scope == VisibilityScope.Public);
        query = ApplyFilters(query, criteria);
        query = sort switch
        {
            "deadline" => query.OrderBy(p => p.Deadline.DateUtc),
            _ => query.OrderByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc)
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).Select(ToSummary).ToListAsync(ct);
        return new PagedResult<JobPostingSummaryView>(items, page.Page, page.PageSize, total);
    }

    public async Task<JobPostingView?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.JobPostings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : ToView(p);
    }

    public async Task<PagedResult<JobPostingSummaryView>> ListByEmployerAsync(Guid employerAccountId, string? status, PageRequest page,
        CancellationToken ct = default)
    {
        var query = _db.JobPostings.AsNoTracking().Where(p => p.EmployerAccountId == employerAccountId);
        if (status is not null && Enum.TryParse<JobPostingStatus>(status, true, out var parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        query = query.OrderByDescending(p => p.UpdatedAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).Select(ToSummary).ToListAsync(ct);
        return new PagedResult<JobPostingSummaryView>(items, page.Page, page.PageSize, total);
    }

    public async Task<PagedResult<JobPostingSummaryView>> ListOpenByEmployerAsync(Guid employerAccountId, PageRequest page, CancellationToken ct = default)
    {
        var query = _db.JobPostings.AsNoTracking()
            .Where(p => p.EmployerAccountId == employerAccountId && p.Status == JobPostingStatus.Active)
            .OrderByDescending(p => p.PublishedAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.PageSize).Select(ToSummary).ToListAsync(ct);
        return new PagedResult<JobPostingSummaryView>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<string>> CheckReferenceUsageAsync(string type, IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        if (codes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var postings = await _db.JobPostings.AsNoTracking().Where(p => p.Status != JobPostingStatus.Archived)
            .Select(p => new { p.CategoryCode, p.Skills, p.RequiredTraining }).ToListAsync(ct);

        bool InUse(string code) => type.ToLowerInvariant() switch
        {
            "skills" => postings.Any(p => p.Skills.Contains(code, StringComparer.OrdinalIgnoreCase)),
            "jobs" => postings.Any(p => string.Equals(p.CategoryCode, code, StringComparison.OrdinalIgnoreCase)),
            "trainings" => postings.Any(p => p.RequiredTraining.Contains(code, StringComparer.OrdinalIgnoreCase)),
            _ => false
        };

        return codes.Where(InUse).ToArray();
    }

    public Task<JobPostingSchemaView> GetSchemaAsync(CancellationToken ct = default) => Task.FromResult(new JobPostingSchemaView(0,
        new[]
        {
            new JobPostingSchemaFieldView("titleAr", true, "string", null), new JobPostingSchemaFieldView("titleEn", true, "string", null),
            new JobPostingSchemaFieldView("summaryAr", true, "string", null), new JobPostingSchemaFieldView("summaryEn", true, "string", null),
            new JobPostingSchemaFieldView("skills", true, "string[]", null),
            new JobPostingSchemaFieldView("categoryCode", true, "string", null),
            new JobPostingSchemaFieldView("contractType", true, "enum", Enum.GetNames<ContractType>()),
            new JobPostingSchemaFieldView("workFormat", true, "enum", Enum.GetNames<WorkFormat>()),
            new JobPostingSchemaFieldView("educationLevel", false, "enum", Enum.GetNames<EducationLevel>()),
            new JobPostingSchemaFieldView("deadlineUtc", true, "date", null)
        }, Array.Empty<string>(), Array.Empty<string>()));

    private static IQueryable<Domain.JobPosting> ApplyFilters(IQueryable<Domain.JobPosting> query, SearchCriteriaInput c)
    {
        if (!string.IsNullOrWhiteSpace(c.Keyword))
        {
            var pattern = $"%{c.Keyword}%";
            query = query.Where(p => EF.Functions.Like(p.Title.En, pattern) || EF.Functions.Like(p.Title.Ar, pattern)
                || EF.Functions.Like(p.Summary.En, pattern) || EF.Functions.Like(p.Summary.Ar, pattern));
        }

        if (c.Governorate is not null)
        {
            query = query.Where(p => p.Location != null && p.Location.Governorate == c.Governorate);
        }

        if (c.City is not null)
        {
            query = query.Where(p => p.Location != null && p.Location.City == c.City);
        }

        if (c.CategoryCode is not null)
        {
            query = query.Where(p => p.CategoryCode == c.CategoryCode);
        }

        if (c.ContractType is not null)
        {
            var contractType = Enum.Parse<ContractType>(c.ContractType, true);
            query = query.Where(p => p.ContractType == contractType);
        }

        if (c.SalaryMin is { } min)
        {
            query = query.Where(p => p.Salary == null || p.Salary.Max == null || p.Salary.Max >= min);
        }

        if (c.SalaryMax is { } max)
        {
            query = query.Where(p => p.Salary == null || p.Salary.Min == null || p.Salary.Min <= max);
        }

        if (c.PostedAfterUtc is { } after)
        {
            query = query.Where(p => (p.PublishedAtUtc ?? p.CreatedAtUtc) >= after);
        }

        if (c.DeadlineBeforeUtc is { } before)
        {
            query = query.Where(p => p.Deadline.DateUtc <= before);
        }

        return query;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Domain.JobPosting, JobPostingSummaryView>> ToSummary = p =>
        new JobPostingSummaryView(p.Id, new LocalizedView(p.Title.Ar, p.Title.En), p.CategoryCode,
            p.Location == null ? null : new JobLocationView(p.Location.Governorate, p.Location.City),
            p.Salary == null ? null : new SalaryRangeView(p.Salary.Min, p.Salary.Max, p.Salary.Currency), p.Deadline.DateUtc, p.Status.ToString(),
            p.ContractType.ToString(), p.PublishedAtUtc, null);

    private static JobPostingView ToView(Domain.JobPosting p) => new(p.Id, p.EmployerAccountId, p.Source.Type.ToString(),
        new LocalizedView(p.Title.Ar, p.Title.En), new LocalizedView(p.Summary.Ar, p.Summary.En), p.Skills, p.CategoryCode, p.ContractType.ToString(),
        p.EducationLevelValue?.ToString(), p.RequiredTraining, p.WorkFormat.ToString(),
        p.Location is null ? null : new JobLocationView(p.Location.Governorate, p.Location.City),
        p.Salary is null ? null : new SalaryRangeView(p.Salary.Min, p.Salary.Max, p.Salary.Currency), p.MinExperienceYears, p.MaxExperienceYears,
        p.RequiredLanguages, p.Deadline.DateUtc, p.Deadline.AutoClose, p.JobLink, p.OtherFields,
        new JobVisibilityView(p.Visibility.Scope.ToString(), p.Visibility.TargetJobSeekerIds), p.Status.ToString(), p.AdminSuspended, p.TaxonomyVersion,
        p.CreatedAtUtc, p.PublishedAtUtc, p.UpdatedAtUtc, p.RowVersion);
}
