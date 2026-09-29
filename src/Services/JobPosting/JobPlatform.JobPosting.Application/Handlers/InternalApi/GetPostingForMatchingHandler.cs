using JobPlatform.JobPosting.Application.Queries.InternalApi;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.InternalApi;

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
