using JobPlatform.CandidateSourcing.Application.DTOs.Insight;
using JobPlatform.CandidateSourcing.Application.Queries.Insight;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.Insight;

internal sealed class GetCandidateInsightHandler : IQueryHandler<GetCandidateInsightQuery, CandidateInsightView>
{
    private readonly IJobPostingApi _postings;
    private readonly IAiMatchingApi _matching;
    private readonly IJobSeekerProfileApi _profiles;
    private readonly ICandidateInsightRepository _insights;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public GetCandidateInsightHandler(IJobPostingApi postings, IAiMatchingApi matching, IJobSeekerProfileApi profiles,
        ICandidateInsightRepository insights, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    {
        _postings = postings;
        _matching = matching;
        _profiles = profiles;
        _insights = insights;
        _unitOfWork = unitOfWork;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<CandidateInsightView>> Handle(GetCandidateInsightQuery request, CancellationToken ct)
    {
        var employerId = ActorFactory.From(_user).Id;
        var posting = await _postings.GetPostingForMatchingAsync(request.JobPostingId, ct);
        if (posting is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
        }

        if (posting.EmployerAccountId != employerId)
        {
            return Error.Forbidden(ErrorCodes.Forbidden, "Only the owning employer may view candidate insight for this posting.");
        }

        var candidateView = await _profiles.GetCandidateViewAsync(request.CandidateProfileId, ct);
        if (candidateView is null || candidateView.Deactivated)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The candidate was not found or is not visible.");
        }

        // Q-03 (resolved): BC-10's MatchScoreComputed/ListMatchScoresAsync is already enriched with profileId + score + the full criterion
        // breakdown, so no separate fetch-on-demand contract was needed for the fit calculation.
        var scores = await _matching.ListMatchScoresAsync(request.JobPostingId, null, 1, 200, ct);
        var match = scores.Items.FirstOrDefault(s => s.ProfileId == request.CandidateProfileId);
        var breakdown = match?.Breakdown ?? Array.Empty<MatchCriterionDto>();
        var included = breakdown.Where(b => b.Included).Select(b => (b.Criterion, b.Score)).ToArray();

        var insight = Domain.Insight.CandidateInsight.Compute(employerId, request.JobPostingId, request.CandidateProfileId, employerId,
            candidateView.DisclosedFields, candidateView.Availability, candidateView.ExpectedSalaryMin, candidateView.ExpectedSalaryMax,
            match?.Score ?? 0m, included.Select(c => (c.Criterion, c.Score, true)).ToArray(), _clock.GetUtcNow().UtcDateTime);

        _insights.Add(insight);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CandidateInsightView(insight.Id, insight.CandidateProfileId, insight.JobPostingId, insight.Availability, insight.ExpectedSalaryMin,
            insight.ExpectedSalaryMax, insight.Fit.OverallScore, insight.Fit.Strengths.ToArray(), insight.Fit.Gaps.ToArray(), insight.WithheldFields, insight.ComputedAtUtc);
    }
}
