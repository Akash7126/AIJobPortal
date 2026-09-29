using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

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
