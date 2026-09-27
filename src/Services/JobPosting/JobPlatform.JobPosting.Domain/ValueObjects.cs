using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain;

public enum JobPostingStatus
{
    Draft,
    Active,
    Paused,
    Expired,
    Archived
}

public enum ContractType
{
    FullTime,
    PartTime,
    Contract,
    Internship,
    Temporary
}

public enum WorkFormat
{
    Physical,
    Online,
    Hybrid
}

public enum EducationLevel
{
    None,
    Primary,
    Secondary,
    Diploma,
    Bachelor,
    Master,
    Doctorate
}

public enum VisibilityScope
{
    Public,
    Private,
    Targeted
}

public enum JobSourceType
{
    Employer,
    External
}

/// <summary>Reference-list entry that another BC (favourite / interested list) can point to.</summary>
public enum InterestedReferenceType
{
    Posting,
    Filter
}

public sealed class SalaryRange : ValueObject
{
    private SalaryRange(decimal? min, decimal? max, string? currency)
    {
        Min = min;
        Max = max;
        Currency = currency;
    }

    public decimal? Min { get; }
    public decimal? Max { get; }
    public string? Currency { get; }

    public static SalaryRange? Create(decimal? min, decimal? max, string? currency) =>
        min is null && max is null ? null : new SalaryRange(min, max, currency);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Min;
        yield return Max;
        yield return Currency;
    }
}

/// <summary>Last apply date and whether the posting auto-closes (moves to Expired) when it passes.</summary>
public sealed class ApplicationDeadline : ValueObject
{
    private ApplicationDeadline(DateTime dateUtc, bool autoClose)
    {
        DateUtc = dateUtc;
        AutoClose = autoClose;
    }

    public DateTime DateUtc { get; }
    public bool AutoClose { get; }

    public static ApplicationDeadline Create(DateTime dateUtc, bool autoClose) =>
        new(DateTime.SpecifyKind(dateUtc, DateTimeKind.Utc), autoClose);

    public bool IsPast(DateTime nowUtc) => DateUtc <= nowUtc;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DateUtc;
        yield return AutoClose;
    }
}

public sealed class JobLocation : ValueObject
{
    private JobLocation(string? governorate, string? city)
    {
        Governorate = governorate;
        City = city;
    }

    public string? Governorate { get; }
    public string? City { get; }

    public static JobLocation? Create(string? governorate, string? city) =>
        governorate is null && city is null ? null : new JobLocation(governorate, city);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Governorate;
        yield return City;
    }
}

/// <summary>Q-03 (proposed): a "targeted" posting is visible only to the listed job seekers.</summary>
public sealed class JobVisibility : ValueObject
{
    private JobVisibility(VisibilityScope scope, IReadOnlyList<Guid> targetJobSeekerIds)
    {
        Scope = scope;
        TargetJobSeekerIds = targetJobSeekerIds;
    }

    public VisibilityScope Scope { get; }
    public IReadOnlyList<Guid> TargetJobSeekerIds { get; }

    public static JobVisibility Public() => new(VisibilityScope.Public, Array.Empty<Guid>());

    public static JobVisibility Private() => new(VisibilityScope.Private, Array.Empty<Guid>());

    public static JobVisibility Targeted(IReadOnlyList<Guid> targetJobSeekerIds) => new(VisibilityScope.Targeted, targetJobSeekerIds);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Scope;
        foreach (var id in TargetJobSeekerIds)
        {
            yield return id;
        }
    }
}

/// <summary>Whether the posting was authored by an employer or imported from an external job site (BC-02), and the import attribution.</summary>
public sealed class JobSource : ValueObject
{
    private JobSource(JobSourceType type, Guid? sourcePlatformId, string? platformJobId, string? sourceName, string? backlinkUrl, bool attributionPublic)
    {
        Type = type;
        SourcePlatformId = sourcePlatformId;
        PlatformJobId = platformJobId;
        SourceName = sourceName;
        BacklinkUrl = backlinkUrl;
        AttributionPublic = attributionPublic;
    }

    public JobSourceType Type { get; }
    public Guid? SourcePlatformId { get; }
    public string? PlatformJobId { get; }
    public string? SourceName { get; }
    public string? BacklinkUrl { get; }
    public bool AttributionPublic { get; }

    public static JobSource FromEmployer() => new(JobSourceType.Employer, null, null, null, null, false);

    public static JobSource FromExternal(Guid sourcePlatformId, string platformJobId, string? sourceName, string? backlinkUrl, bool attributionPublic) =>
        new(JobSourceType.External, sourcePlatformId, platformJobId, sourceName, backlinkUrl, attributionPublic);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return SourcePlatformId;
        yield return PlatformJobId;
        yield return SourceName;
        yield return BacklinkUrl;
        yield return AttributionPublic;
    }
}

/// <summary>The 8 search filters of US-3.2.2-01, immutable so a saved search can hash and replay it (foundation section 8: CriteriaHash).</summary>
public sealed class SearchCriteria : ValueObject
{
    public SearchCriteria(string? keyword, string? governorate, string? city, decimal? salaryMin, decimal? salaryMax, ContractType? contractType,
        DateTime? postedAfterUtc, DateTime? deadlineBeforeUtc, string? categoryCode)
    {
        Keyword = keyword?.Trim();
        Governorate = governorate;
        City = city;
        SalaryMin = salaryMin;
        SalaryMax = salaryMax;
        ContractType = contractType;
        PostedAfterUtc = postedAfterUtc;
        DeadlineBeforeUtc = deadlineBeforeUtc;
        CategoryCode = categoryCode;
    }

    public string? Keyword { get; }
    public string? Governorate { get; }
    public string? City { get; }
    public decimal? SalaryMin { get; }
    public decimal? SalaryMax { get; }
    public ContractType? ContractType { get; }
    public DateTime? PostedAfterUtc { get; }
    public DateTime? DeadlineBeforeUtc { get; }
    public string? CategoryCode { get; }

    /// <summary>Pure predicate reused by search and by <see cref="SavedSearch"/> match detection (US-3.2.2-04 AC-04).</summary>
    public bool Matches(JobPosting posting)
    {
        if (posting.Status != JobPostingStatus.Active)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var haystack = posting.Title.En + " " + posting.Title.Ar + " " + posting.Summary.En + " " + posting.Summary.Ar;
            if (haystack.Contains(Keyword, StringComparison.OrdinalIgnoreCase) is false)
            {
                return false;
            }
        }

        if (Governorate is not null && !string.Equals(posting.Location?.Governorate, Governorate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (City is not null && !string.Equals(posting.Location?.City, City, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (CategoryCode is not null && !string.Equals(posting.CategoryCode, CategoryCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ContractType is { } ct && posting.ContractType != ct)
        {
            return false;
        }

        if (SalaryMin is { } min && (posting.Salary?.Max ?? decimal.MaxValue) < min)
        {
            return false;
        }

        if (SalaryMax is { } max && (posting.Salary?.Min ?? decimal.MinValue) > max)
        {
            return false;
        }

        if (DeadlineBeforeUtc is { } before && posting.Deadline.DateUtc > before)
        {
            return false;
        }

        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Keyword?.ToLowerInvariant();
        yield return Governorate?.ToLowerInvariant();
        yield return City?.ToLowerInvariant();
        yield return SalaryMin;
        yield return SalaryMax;
        yield return ContractType;
        yield return PostedAfterUtc;
        yield return DeadlineBeforeUtc;
        yield return CategoryCode?.ToLowerInvariant();
    }
}

/// <summary>A bookmark of either an existing posting or a stored filter (US-3.2.3-01).</summary>
public sealed class InterestedReference : ValueObject
{
    private InterestedReference(InterestedReferenceType type, Guid? postingId, SearchCriteria? criteria)
    {
        Type = type;
        PostingId = postingId;
        Criteria = criteria;
    }

    public InterestedReferenceType Type { get; }
    public Guid? PostingId { get; }
    public SearchCriteria? Criteria { get; }

    public static InterestedReference ToPosting(Guid postingId) => new(InterestedReferenceType.Posting, postingId, null);

    public static InterestedReference ToFilter(SearchCriteria criteria) => new(InterestedReferenceType.Filter, null, criteria);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return PostingId;
        yield return Criteria;
    }
}
