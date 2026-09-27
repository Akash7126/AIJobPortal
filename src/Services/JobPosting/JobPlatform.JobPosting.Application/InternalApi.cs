using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application;

/// <summary>Service-to-service queries behind <c>/internal/v1</c> (handover section 6.1); implements <see cref="IJobPostingApi"/> and the
/// reference-usage contract for BC-08.</summary>
public sealed record GetPostingForMatchingQuery(Guid JobPostingId) : ServiceQuery<PostingForMatchingDto?>;

public sealed record ListOpenPostingsByEmployerQuery(Guid EmployerAccountId, int Page = 1, int PageSize = 20) : ServiceQuery<PagedResult<JobPostingSummaryView>>;

public sealed record CheckReferenceUsageQuery(string Type, IReadOnlyList<string> Codes) : ServiceQuery<IReadOnlyList<string>>;

internal sealed class GetPostingForMatchingHandler : IQueryHandler<GetPostingForMatchingQuery, PostingForMatchingDto?>
{
    private readonly IJobPostingRepository _postings;

    public GetPostingForMatchingHandler(IJobPostingRepository postings) => _postings = postings;

    public async Task<Result<PostingForMatchingDto?>> Handle(GetPostingForMatchingQuery request, CancellationToken ct)
    {
        var p = await _postings.GetForMatchEvaluationAsync(request.JobPostingId, ct);
        if (p is null)
        {
            return Result.Success<PostingForMatchingDto?>(null);
        }

        return new PostingForMatchingDto(p.Id, p.EmployerAccountId ?? Guid.Empty, p.Status.ToString(), p.AdminSuspended, p.Version, p.Title.En,
            p.CategoryCode, p.Summary.En, p.Skills, p.EducationLevelValue?.ToString(), p.RequiredTraining, p.Location?.Governorate, p.Location?.City,
            p.WorkFormat.ToString(), p.MinExperienceYears, p.MaxExperienceYears, p.Salary?.Min, p.Salary?.Max);
    }
}

internal sealed class ListOpenPostingsByEmployerHandler : IQueryHandler<ListOpenPostingsByEmployerQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IJobPostingSearchReadModel _search;

    public ListOpenPostingsByEmployerHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(ListOpenPostingsByEmployerQuery request, CancellationToken ct) =>
        await _search.ListOpenByEmployerAsync(request.EmployerAccountId, new PageRequest(request.Page, request.PageSize), ct);
}

internal sealed class CheckReferenceUsageHandler : IQueryHandler<CheckReferenceUsageQuery, IReadOnlyList<string>>
{
    private readonly IJobPostingSearchReadModel _search;

    public CheckReferenceUsageHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<IReadOnlyList<string>>> Handle(CheckReferenceUsageQuery request, CancellationToken ct) =>
        Result.Success(await _search.CheckReferenceUsageAsync(request.Type, request.Codes, ct));
}
