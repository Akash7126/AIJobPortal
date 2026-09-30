using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using JobPlatform.JobPosting.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

internal sealed class CreateJobPostingHandler : ICommandHandler<CreateJobPostingCommand, PostingMutationResult>
{
    private readonly IJobPostingRepository _postings;
    private readonly IJobPostingSchemaValidator _schema;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CreateJobPostingHandler(IJobPostingRepository postings, Domain.Interfaces.Services.IJobPostingSchemaValidator schema, ICurrentUser user, TimeProvider clock)
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
