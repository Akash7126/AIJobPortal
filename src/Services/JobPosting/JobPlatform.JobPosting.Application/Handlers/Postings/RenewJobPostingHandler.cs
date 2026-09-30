using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

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
