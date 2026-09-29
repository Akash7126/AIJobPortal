using JobPlatform.JobPosting.Application.Commands.Interested;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Interested;

internal sealed class AddInterestedListEntryHandler : ICommandHandler<AddInterestedListEntryCommand, Guid>
{
    private readonly IInterestedListRepository _entries;
    private readonly IJobPostingRepository _postings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AddInterestedListEntryHandler(IInterestedListRepository entries, IJobPostingRepository postings, ICurrentUser user, TimeProvider clock)
    {
        _entries = entries;
        _postings = postings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(AddInterestedListEntryCommand request, CancellationToken ct)
    {
        var owner = _user.UserId!.Value;
        var reference = request.ToReference();
        if (reference.Type == InterestedReferenceType.Posting)
        {
            var posting = await _postings.GetByIdAsync(reference.PostingId!.Value, ct);
            if (posting is null)
            {
                return Error.NotFound(ErrorCodes.NotFound, "The job posting was not found.");
            }
        }

        var existing = await _entries.ListByOwnerAsync(owner, ct);
        var duplicate = existing.FirstOrDefault(e => e.Matches(reference));
        if (duplicate is not null)
        {
            return duplicate.Id;
        }

        var entry = InterestedListEntry.Add(owner, reference, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        _entries.Add(entry);
        return entry.Id;
    }
}
