using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Application.Queries.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

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
