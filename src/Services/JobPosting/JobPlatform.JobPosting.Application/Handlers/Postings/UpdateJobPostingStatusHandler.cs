using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Application.Interfaces;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

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
