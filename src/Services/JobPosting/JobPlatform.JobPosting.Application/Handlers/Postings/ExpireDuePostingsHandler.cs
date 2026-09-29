using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

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
