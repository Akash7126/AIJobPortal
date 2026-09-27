using FluentValidation;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Application;

// ---------------------------------------------------------------------- commands

public sealed record CreateJobPostingCommand(
    string TitleAr, string TitleEn, string SummaryAr, string SummaryEn, IReadOnlyList<string> Skills, string CategoryCode, string ContractType,
    string? EducationLevel, IReadOnlyList<string>? RequiredTraining, string WorkFormat, string? Governorate, string? City, decimal? SalaryMin,
    decimal? SalaryMax, string? SalaryCurrency, int? MinExperienceYears, int? MaxExperienceYears, IReadOnlyList<string>? RequiredLanguages,
    DateTime DeadlineUtc, bool AutoClose, string? JobLink, IReadOnlyDictionary<string, string>? OtherFields, string? IdempotencyKey)
    : EmployerCommand<PostingMutationResult>, IIdempotentCommand
{
    public JobPostingFields ToFields() => new(
        new SharedKernel.Common.ValueObjects.LocalizedText(TitleAr, TitleEn), new SharedKernel.Common.ValueObjects.LocalizedText(SummaryAr, SummaryEn),
        Skills, CategoryCode, Enum.Parse<ContractType>(ContractType, true), EducationLevel is null ? null : Enum.Parse<EducationLevel>(EducationLevel, true),
        RequiredTraining ?? Array.Empty<string>(), Enum.Parse<WorkFormat>(WorkFormat, true), Domain.JobLocation.Create(Governorate, City),
        SalaryRange.Create(SalaryMin, SalaryMax, SalaryCurrency), MinExperienceYears, MaxExperienceYears, RequiredLanguages ?? Array.Empty<string>(),
        ApplicationDeadline.Create(DeadlineUtc, AutoClose), JobLink, OtherFields);
}

public sealed record UpdateJobPostingCommand(
    Guid JobPostingId, string TitleAr, string TitleEn, string SummaryAr, string SummaryEn, IReadOnlyList<string> Skills, string CategoryCode,
    string ContractType, string? EducationLevel, IReadOnlyList<string>? RequiredTraining, string WorkFormat, string? Governorate, string? City,
    decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency, int? MinExperienceYears, int? MaxExperienceYears,
    IReadOnlyList<string>? RequiredLanguages, DateTime DeadlineUtc, bool AutoClose, string? JobLink, IReadOnlyDictionary<string, string>? OtherFields,
    string VisibilityScope, IReadOnlyList<Guid>? TargetJobSeekerIds, string? IfMatch) : EmployerCommand<PostingMutationResult>
{
    public JobPostingFields ToFields() => new(
        new SharedKernel.Common.ValueObjects.LocalizedText(TitleAr, TitleEn), new SharedKernel.Common.ValueObjects.LocalizedText(SummaryAr, SummaryEn),
        Skills, CategoryCode, Enum.Parse<ContractType>(ContractType, true), EducationLevel is null ? null : Enum.Parse<EducationLevel>(EducationLevel, true),
        RequiredTraining ?? Array.Empty<string>(), Enum.Parse<WorkFormat>(WorkFormat, true), Domain.JobLocation.Create(Governorate, City),
        SalaryRange.Create(SalaryMin, SalaryMax, SalaryCurrency), MinExperienceYears, MaxExperienceYears, RequiredLanguages ?? Array.Empty<string>(),
        ApplicationDeadline.Create(DeadlineUtc, AutoClose), JobLink, OtherFields);

    public JobVisibility ToVisibility()
    {
        var scope = Enum.Parse<Domain.VisibilityScope>(VisibilityScope, true);
        return scope switch
        {
            Domain.VisibilityScope.Targeted => JobVisibility.Targeted(TargetJobSeekerIds ?? Array.Empty<Guid>()),
            Domain.VisibilityScope.Private => JobVisibility.Private(),
            _ => JobVisibility.Public()
        };
    }
}

public sealed record RenewJobPostingCommand(Guid JobPostingId, DateTime NewDeadlineUtc) : EmployerCommand<PostingMutationResult>;

public sealed record UpdateJobPostingStatusCommand(Guid JobPostingId, string Status) : EmployerStatusCommand<Unit>;

/// <summary>Scheduled (US-3.2.1-01 AC-02): moves due postings with auto-close enabled to Expired. Not exposed over HTTP.</summary>
public sealed record ExpireDuePostingsCommand(int BatchSize = 200) : ICommand<int>;

// ---------------------------------------------------------------------- queries

public sealed record SearchJobPostingsQuery(
    string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType, DateTime? PostedAfterUtc,
    DateTime? DeadlineBeforeUtc, string? CategoryCode, string? Sort, int Page = 1, int PageSize = 20) : PublicQuery<PagedResult<JobPostingSummaryView>>
{
    public SearchCriteriaInput ToCriteria() => new(Keyword, Governorate, City, SalaryMin, SalaryMax, ContractType, PostedAfterUtc, DeadlineBeforeUtc, CategoryCode);
}

public sealed record GetJobPostingQuery(Guid JobPostingId) : PublicQuery<JobPostingView>;

public sealed record ListMyJobPostingsQuery(string? Status, int Page = 1, int PageSize = 20) : EmployerQuery<PagedResult<JobPostingSummaryView>>;

public sealed record GetRecommendedJobsQuery(int Page = 1, int PageSize = 20) : JobSeekerQuery<PagedResult<JobPostingSummaryView>>;

public sealed record GetJobPostingSchemaQuery : EmployerQuery<JobPostingSchemaView>;

// ---------------------------------------------------------------------- validators

public static class PostingFieldRules
{
    public const int TitleMin = 3, TitleMax = 200;
    public const int SummaryMin = 20, SummaryMax = 5000;
    public const int MinSkills = 1, MaxSkills = 30;
    public static readonly string[] RequiredLanguageCodes = { "ar", "en" };
}

file static class SharedFieldRules
{
    public static void Apply<T>(AbstractValidator<T> v, Func<T, string> ar, Func<T, string> en, Func<T, string> summaryAr, Func<T, string> summaryEn,
        Func<T, IReadOnlyList<string>> skills, Func<T, string?> jobLink, Func<T, DateTime> deadline, Func<T, IReadOnlyList<string>?> requiredLanguages,
        Func<T, decimal?> salaryMin, Func<T, decimal?> salaryMax, Func<T, IReadOnlyDictionary<string, string>?> otherFields)
    {
        v.RuleFor(x => ar(x)).NotEmpty().WithErrorCode("VAL.TitleAr.Required")
            .Length(PostingFieldRules.TitleMin, PostingFieldRules.TitleMax).WithErrorCode("VAL.TitleAr.OutOfRange");
        v.RuleFor(x => en(x)).NotEmpty().WithErrorCode("VAL.TitleEn.Required")
            .Length(PostingFieldRules.TitleMin, PostingFieldRules.TitleMax).WithErrorCode("VAL.TitleEn.OutOfRange");
        v.RuleFor(x => summaryAr(x)).NotEmpty().WithErrorCode("VAL.SummaryAr.Required")
            .Length(PostingFieldRules.SummaryMin, PostingFieldRules.SummaryMax).WithErrorCode("VAL.SummaryAr.OutOfRange");
        v.RuleFor(x => summaryEn(x)).NotEmpty().WithErrorCode("VAL.SummaryEn.Required")
            .Length(PostingFieldRules.SummaryMin, PostingFieldRules.SummaryMax).WithErrorCode("VAL.SummaryEn.OutOfRange");
        v.RuleFor(x => skills(x)).Must(s => s is { Count: >= PostingFieldRules.MinSkills and <= PostingFieldRules.MaxSkills })
            .WithErrorCode("VAL.Skills.OutOfRange");
        v.RuleFor(x => jobLink(x)).Must(link => link is null || Uri.TryCreate(link, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            .WithErrorCode("VAL.JobLink.Invalid");
        v.RuleFor(x => deadline(x)).Must((x, d) => salaryMin(x) is null || salaryMax(x) is null || salaryMin(x) <= salaryMax(x))
            .WithErrorCode("VAL.Salary.MinGreaterThanMax");
        v.RuleFor(x => requiredLanguages(x)).Must(langs => langs is null || langs.All(l => PostingFieldRules.RequiredLanguageCodes.Contains(l.ToLowerInvariant())))
            .WithErrorCode("VAL.RequiredLanguages.Invalid");
        v.RuleFor(x => otherFields(x)).Must(f => f is null || f.Count <= 20).WithErrorCode("VAL.OtherFields.TooMany");
    }
}

public sealed class CreateJobPostingValidator : AbstractValidator<CreateJobPostingCommand>
{
    public CreateJobPostingValidator()
    {
        SharedFieldRules.Apply(this, c => c.TitleAr, c => c.TitleEn, c => c.SummaryAr, c => c.SummaryEn, c => c.Skills, c => c.JobLink, c => c.DeadlineUtc,
            c => c.RequiredLanguages, c => c.SalaryMin, c => c.SalaryMax, c => c.OtherFields);
        RuleFor(c => c.ContractType).IsEnumName(typeof(ContractType), false).WithErrorCode("VAL.ContractType.Invalid");
        RuleFor(c => c.WorkFormat).IsEnumName(typeof(WorkFormat), false).WithErrorCode("VAL.WorkFormat.Invalid");
        RuleFor(c => c.EducationLevel).Must(v => v is null || Enum.TryParse<EducationLevel>(v, true, out _)).WithErrorCode("VAL.EducationLevel.Invalid");
        RuleFor(c => c.DeadlineUtc).GreaterThan(DateTime.UtcNow).WithErrorCode("VAL.Deadline.MustBeFuture");
    }
}

public sealed class UpdateJobPostingValidator : AbstractValidator<UpdateJobPostingCommand>
{
    public UpdateJobPostingValidator()
    {
        SharedFieldRules.Apply(this, c => c.TitleAr, c => c.TitleEn, c => c.SummaryAr, c => c.SummaryEn, c => c.Skills, c => c.JobLink, c => c.DeadlineUtc,
            c => c.RequiredLanguages, c => c.SalaryMin, c => c.SalaryMax, c => c.OtherFields);
        RuleFor(c => c.ContractType).IsEnumName(typeof(ContractType), false).WithErrorCode("VAL.ContractType.Invalid");
        RuleFor(c => c.WorkFormat).IsEnumName(typeof(WorkFormat), false).WithErrorCode("VAL.WorkFormat.Invalid");
        RuleFor(c => c.VisibilityScope).IsEnumName(typeof(VisibilityScope), false).WithErrorCode("VAL.VisibilityScope.Invalid");
    }
}

public sealed class RenewJobPostingValidator : AbstractValidator<RenewJobPostingCommand>
{
    public RenewJobPostingValidator()
    {
        RuleFor(c => c.NewDeadlineUtc).GreaterThan(DateTime.UtcNow).WithErrorCode("VAL.Deadline.MustBeFuture")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMonths(12)).WithErrorCode("VAL.Deadline.TooFarAhead");
    }
}

public sealed class UpdateJobPostingStatusValidator : AbstractValidator<UpdateJobPostingStatusCommand>
{
    public UpdateJobPostingStatusValidator()
    {
        RuleFor(c => c.Status).IsEnumName(typeof(JobPostingStatus), false).WithErrorCode("VAL.Status.Invalid");
    }
}

public sealed class SearchJobPostingsValidator : AbstractValidator<SearchJobPostingsQuery>
{
    private static readonly string[] AllowedSorts = { "relevance", "newest", "deadline" };

    public SearchJobPostingsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");
        RuleFor(q => q.Keyword).MaximumLength(100).WithErrorCode("VAL.Keyword.TooLong");
        RuleFor(q => q).Must(q => q.SalaryMin is null || q.SalaryMax is null || q.SalaryMin <= q.SalaryMax)
            .WithErrorCode(ErrorCodes.SearchInvalidField).WithName("salary");
        RuleFor(q => q.Sort).Must(s => s is null || AllowedSorts.Contains(s)).WithErrorCode("VAL.Sort.Invalid");
        RuleFor(q => q.ContractType).IsEnumName(typeof(ContractType), false).WithErrorCode("VAL.ContractType.Invalid").When(q => q.ContractType is not null);
    }
}

// ---------------------------------------------------------------------- handlers

internal sealed class CreateJobPostingHandler : ICommandHandler<CreateJobPostingCommand, PostingMutationResult>
{
    private readonly IJobPostingRepository _postings;
    private readonly IJobPostingSchemaValidator _schema;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CreateJobPostingHandler(IJobPostingRepository postings, Domain.IJobPostingSchemaValidator schema, ICurrentUser user, TimeProvider clock)
    {
        _postings = postings;
        _schema = schema;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<PostingMutationResult>> Handle(CreateJobPostingCommand request, CancellationToken ct)
    {
        var fields = request.ToFields();
        var validation = await ValidateOrTimeoutAsync(_schema, fields, ct);
        SchemaValidationGuard.EnsureValid(validation);

        var employerId = _user.UserId!.Value;
        var hash = ContentHasher.Hash(fields);
        var existing = await _postings.GetDraftByHashAsync(employerId, hash, ct);
        if (existing is not null)
        {
            return new PostingMutationResult(existing.Id, Existing: true);
        }

        var posting = Domain.JobPosting.CreateDraft(employerId, fields, ActorFactory.From(_user), validation.TaxonomyVersion, hash, _clock.GetUtcNow().UtcDateTime);
        _postings.Add(posting);
        return new PostingMutationResult(posting.Id);
    }

    internal static async Task<SchemaValidationResult> ValidateOrTimeoutAsync(IJobPostingSchemaValidator schema, JobPostingFields fields, CancellationToken ct)
    {
        try
        {
            return await schema.ValidateAsync(fields, ct);
        }
        catch (TaxonomyUnavailableException ex)
        {
            throw new BusinessRuleViolationException(RuleCodes.PostingTaxonomyTimeout, ex.Message, ErrorCodes.UpstreamTimeout, BusinessRuleKind.BusinessRule);
        }
    }
}

internal sealed class UpdateJobPostingHandler : ICommandHandler<UpdateJobPostingCommand, PostingMutationResult>
{
    private readonly IJobPostingRepository _postings;
    private readonly IJobPostingSchemaValidator _schema;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdateJobPostingHandler(IJobPostingRepository postings, IJobPostingSchemaValidator schema, ICurrentUser user, TimeProvider clock)
    {
        _postings = postings;
        _schema = schema;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<PostingMutationResult>> Handle(UpdateJobPostingCommand request, CancellationToken ct)
    {
        var posting = await _postings.GetByIdAsync(request.JobPostingId, ct);
        if (posting is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
        }

        if (!ETag.Matches(request.IfMatch, posting.RowVersion))
        {
            return Error.PreconditionFailed("E-PRECONDITION-FAILED", "The posting was modified since it was last read.");
        }

        var fields = request.ToFields();
        var validation = await CreateJobPostingHandler.ValidateOrTimeoutAsync(_schema, fields, ct);
        SchemaValidationGuard.EnsureValid(validation);

        // Later-save-wins (handover section 3.1): a caller that skips If-Match accepts that its save may replace someone else's concurrent edit;
        // the response flags this so the UI can inform the employer (a caller that supplies If-Match already got a 412 above on a stale read).
        var overwritten = string.IsNullOrEmpty(request.IfMatch);
        var actor = ActorFactory.From(_user);
        posting.Edit(fields, actor, validation.TaxonomyVersion, ContentHasher.Hash(fields), _clock.GetUtcNow().UtcDateTime);
        posting.SetVisibility(request.ToVisibility(), actor, _clock.GetUtcNow().UtcDateTime);
        return new PostingMutationResult(posting.Id, Overwritten: overwritten);
    }
}

internal sealed class RenewJobPostingHandler : ICommandHandler<RenewJobPostingCommand, PostingMutationResult>
{
    private readonly IJobPostingRepository _postings;
    private readonly Events.SavedSearchMatchEvaluator _matcher;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RenewJobPostingHandler(IJobPostingRepository postings, Events.SavedSearchMatchEvaluator matcher, ICurrentUser user, TimeProvider clock)
    {
        _postings = postings;
        _matcher = matcher;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<PostingMutationResult>> Handle(RenewJobPostingCommand request, CancellationToken ct)
    {
        var posting = await _postings.GetByIdAsync(request.JobPostingId, ct);
        if (posting is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        posting.Renew(ApplicationDeadline.Create(request.NewDeadlineUtc, true), ActorFactory.From(_user), now);
        await _matcher.EvaluateAsync(posting, now, ct);
        return new PostingMutationResult(posting.Id);
    }
}

internal sealed class UpdateJobPostingStatusHandler : ICommandHandler<UpdateJobPostingStatusCommand, Unit>
{
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerStandingProvider _standing;
    private readonly Events.SavedSearchMatchEvaluator _matcher;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public UpdateJobPostingStatusHandler(IJobPostingRepository postings, IEmployerStandingProvider standing, Events.SavedSearchMatchEvaluator matcher,
        ICurrentUser user, TimeProvider clock)
    {
        _postings = postings;
        _standing = standing;
        _matcher = matcher;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(UpdateJobPostingStatusCommand request, CancellationToken ct)
    {
        var posting = await _postings.GetByIdAsync(request.JobPostingId, ct);
        if (posting is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
        }

        var to = Enum.Parse<JobPostingStatus>(request.Status, true);
        if (to == JobPostingStatus.Active && posting.Status == JobPostingStatus.Draft && posting.EmployerAccountId is { } employerId)
        {
            var approved = await _standing.IsApprovedAsync(employerId, ct);
            if (!EmployerEligibilityPolicy.MayPost(approved))
            {
                return Error.Forbidden(ErrorCodes.PostingForbidden, "The employer is not approved to publish job postings.");
            }
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        posting.ChangeStatus(to, ActorFactory.From(_user), now);
        await _matcher.EvaluateAsync(posting, now, ct);
        return Result.Success();
    }
}

internal sealed class ExpireDuePostingsHandler : ICommandHandler<ExpireDuePostingsCommand, int>
{
    private readonly IJobPostingRepository _postings;
    private readonly TimeProvider _clock;

    public ExpireDuePostingsHandler(IJobPostingRepository postings, TimeProvider clock)
    {
        _postings = postings;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(ExpireDuePostingsCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var due = await _postings.ListDueForExpiryAsync(now, request.BatchSize, ct);
        foreach (var posting in due)
        {
            posting.Expire(Actor.System(Guid.Empty), now);
        }

        return due.Count;
    }
}

internal sealed class SearchJobPostingsHandler : IQueryHandler<SearchJobPostingsQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IJobPostingSearchReadModel _search;

    public SearchJobPostingsHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(SearchJobPostingsQuery request, CancellationToken ct) =>
        await _search.SearchAsync(request.ToCriteria(), request.Sort, new PageRequest(request.Page, request.PageSize), ct);
}

internal sealed class GetJobPostingHandler : IQueryHandler<GetJobPostingQuery, JobPostingView>
{
    private readonly IJobPostingSearchReadModel _search;
    private readonly ICurrentUser _user;

    public GetJobPostingHandler(IJobPostingSearchReadModel search, ICurrentUser user)
    {
        _search = search;
        _user = user;
    }

    public async Task<Result<JobPostingView>> Handle(GetJobPostingQuery request, CancellationToken ct)
    {
        var view = await _search.GetAsync(request.JobPostingId, ct);
        if (view is null || !CanSee(view))
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
        }

        return view;
    }

    /// <summary>Visibility filtering (handover section 6.1: "any, visibility-filtered"): the owner always sees it; a private posting is
    /// otherwise hidden; a targeted posting is visible only to the listed job seekers.</summary>
    private bool CanSee(JobPostingView view)
    {
        if (_user.UserId is { } id && view.EmployerAccountId == id)
        {
            return true;
        }

        return view.Visibility.Scope switch
        {
            "Private" => false,
            "Targeted" => _user.UserId is { } jobSeekerId && view.Visibility.TargetJobSeekerIds.Contains(jobSeekerId),
            _ => true
        };
    }
}

internal sealed class ListMyJobPostingsHandler : IQueryHandler<ListMyJobPostingsQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IJobPostingSearchReadModel _search;
    private readonly ICurrentUser _user;

    public ListMyJobPostingsHandler(IJobPostingSearchReadModel search, ICurrentUser user)
    {
        _search = search;
        _user = user;
    }

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(ListMyJobPostingsQuery request, CancellationToken ct) =>
        await _search.ListByEmployerAsync(_user.UserId!.Value, request.Status, new PageRequest(request.Page, request.PageSize), ct);
}

internal sealed class GetRecommendedJobsHandler : IQueryHandler<GetRecommendedJobsQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IMatchRankingProvider _ranking;
    private readonly IJobPostingSearchReadModel _search;
    private readonly ICurrentUser _user;

    public GetRecommendedJobsHandler(IMatchRankingProvider ranking, IJobPostingSearchReadModel search, ICurrentUser user)
    {
        _ranking = ranking;
        _search = search;
        _user = user;
    }

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(GetRecommendedJobsQuery request, CancellationToken ct)
    {
        var ranked = await _ranking.GetRankingAsync(_user.UserId!.Value, request.Page, request.PageSize, ct);
        if (ranked.Count == 0)
        {
            // Degrade to plain (unranked) search results when BC-10 is unavailable (handover section 6.2).
            return await _search.SearchAsync(new SearchCriteriaInput(null, null, null, null, null, null, null, null, null), "newest",
                new PageRequest(request.Page, request.PageSize), ct);
        }

        var items = new List<JobPostingSummaryView>();
        foreach (var item in ranked)
        {
            var view = await _search.GetAsync(item.JobPostingId, ct);
            if (view is not null)
            {
                items.Add(new JobPostingSummaryView(view.JobPostingId, view.Title, view.CategoryCode, view.Location, view.Salary, view.DeadlineUtc,
                    view.Status, view.ContractType, view.PublishedAtUtc, item.Score));
            }
        }

        return new PagedResult<JobPostingSummaryView>(items, request.Page, request.PageSize, items.Count);
    }
}

internal sealed class GetJobPostingSchemaHandler : IQueryHandler<GetJobPostingSchemaQuery, JobPostingSchemaView>
{
    private readonly IJobPostingSearchReadModel _search;

    public GetJobPostingSchemaHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<JobPostingSchemaView>> Handle(GetJobPostingSchemaQuery request, CancellationToken ct) => await _search.GetSchemaAsync(ct);
}
