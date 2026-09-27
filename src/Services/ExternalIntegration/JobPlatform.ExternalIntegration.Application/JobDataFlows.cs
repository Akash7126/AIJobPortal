using FluentValidation;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application;

// ---------------------------------------------------------------------- push (US-3.1.3-03)

/// <summary>The push model (handover Q-01): the partner posts a job already shaped like the platform's standard schema, so no
/// JobDataMapping translation is needed on this path (mapping only translates a pull source's own, non-standard shape).</summary>
public sealed record PushJobDataCommand(
    string SourceJobId, string Title, string Summary, IReadOnlyList<string> Skills, string ContractType, string WorkFormat,
    DateTime ApplicationDeadline, string Location, string? SourceUrl, string? IdempotencyKey)
    : PartnerCommand<PushJobDataResultView>, IIdempotentCommand, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => ErrorCodes.JobDataInvalidField;
}

public sealed class PushJobDataValidator : AbstractValidator<PushJobDataCommand>
{
    public PushJobDataValidator()
    {
        RuleFor(c => c.SourceJobId).NotEmpty().MaximumLength(100).WithErrorCode("VAL.SourceJobId.Required");
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Title.Required");
        RuleFor(c => c.Summary).NotEmpty().MaximumLength(5000).WithErrorCode("VAL.Summary.Required");
        RuleFor(c => c.Skills).NotEmpty().WithErrorCode("VAL.Skills.Required");
        RuleFor(c => c.ContractType).NotEmpty().WithErrorCode("VAL.ContractType.Required");
        RuleFor(c => c.WorkFormat).NotEmpty().WithErrorCode("VAL.WorkFormat.Required");
        RuleFor(c => c.Location).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Location.Required");
        RuleFor(c => c.SourceUrl).Must(BeAnAbsoluteHttpsUrl).When(c => c.SourceUrl is not null).WithErrorCode("VAL.SourceUrl.Invalid");
    }

    private static bool BeAnAbsoluteHttpsUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

internal sealed class PushJobDataHandler : ICommandHandler<PushJobDataCommand, PushJobDataResultView>
{
    private readonly IExternalJobSiteIntegrationRepository _integrations;
    private readonly IJobDataRepository _jobData;
    private readonly IJobPostAttributionRepository _attributions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PushJobDataHandler(IExternalJobSiteIntegrationRepository integrations, IJobDataRepository jobData,
        IJobPostAttributionRepository attributions, ICurrentUser user, TimeProvider clock)
    {
        _integrations = integrations;
        _jobData = jobData;
        _attributions = attributions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<PushJobDataResultView>> Handle(PushJobDataCommand request, CancellationToken ct)
    {
        var partnerAccountId = _user.UserId!.Value;
        var integration = await _integrations.GetByPartnerAccountAsync(partnerAccountId, ct);
        if (integration is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
        }

        if (integration.Status != IntegrationStatus.Active || !integration.Models.PushEnabled)
        {
            return Error.Forbidden(ErrorCodes.IntegrationNotActive, "This integration does not have the push model enabled.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var standardJob = new StandardJob(request.Title, request.Summary, request.Skills, request.ContractType, request.WorkFormat,
            request.ApplicationDeadline, request.Location, request.SourceUrl);

        var existing = await _jobData.GetBySourceKeyAsync(integration.SourcePlatform.Id, request.SourceJobId, ct);
        var isUpdate = existing is not null;
        var rawPayload = System.Text.Json.JsonSerializer.Serialize(request);
        JobData jobData;
        if (existing is null)
        {
            jobData = JobData.Receive(Guid.NewGuid(), integration.Id, integration.SourcePlatform.Id, request.SourceJobId, rawPayload,
                JobDataModel.Push, now);
            _jobData.Add(jobData);
        }
        else
        {
            jobData = existing;
            jobData.UpdateRaw(rawPayload, null, now);
        }

        jobData.Standardize(standardJob);
        var wasNew = jobData.PlatformJobId is null;
        jobData.Accept(ActorFactory.From(_user).Id, integration.AttributionVisibility.ToString(), now);
        if (wasNew)
        {
            _attributions.Add(JobPostAttribution.Tag(Guid.NewGuid(), jobData.Id, jobData.PlatformJobId!, integration.SourcePlatform.Name,
                request.SourceUrl, ActorFactory.From(_user).Id, now));
        }

        var confirmation = isUpdate ? "Job data updated." : "Job data imported.";
        return new PushJobDataResultView(jobData.PlatformJobId!, !isUpdate, confirmation);
    }
}

// ---------------------------------------------------------------------- attribution sync (US-3.1.3-09)

public sealed record SyncJobPostAttributionCommand(string PlatformJobId, string Operation, DateTime? Deadline, string? Description)
    : PartnerCommand<Unit>;

public sealed class SyncJobPostAttributionValidator : AbstractValidator<SyncJobPostAttributionCommand>
{
    public SyncJobPostAttributionValidator()
    {
        RuleFor(c => c.PlatformJobId).NotEmpty().WithErrorCode("VAL.PlatformJobId.Required");
        RuleFor(c => c.Operation).Must(o => Enum.TryParse<AttributionOperation>(o, true, out _)).WithErrorCode("VAL.Operation.Invalid");
        When(c => Enum.TryParse<AttributionOperation>(c.Operation, true, out var op) && op == AttributionOperation.ExtendDeadline, () =>
            RuleFor(c => c.Deadline).NotNull().WithErrorCode("VAL.Deadline.Required"));
        When(c => Enum.TryParse<AttributionOperation>(c.Operation, true, out var op) && op == AttributionOperation.EditDescription, () =>
        {
            RuleFor(c => c.Description).NotEmpty().WithErrorCode("VAL.Description.Required");
            RuleFor(c => c.Description).MaximumLength(10_000).WithErrorCode("VAL.Description.TooLong");
        });
    }
}

internal sealed class SyncJobPostAttributionHandler : ICommandHandler<SyncJobPostAttributionCommand, Unit>
{
    private readonly IJobPostAttributionRepository _attributions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SyncJobPostAttributionHandler(IJobPostAttributionRepository attributions, ICurrentUser user, TimeProvider clock)
    {
        _attributions = attributions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(SyncJobPostAttributionCommand request, CancellationToken ct)
    {
        var attribution = await _attributions.GetByPlatformJobIdAsync(request.PlatformJobId, ct);
        if (attribution is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No attribution was found for this platform job id.");
        }

        var actor = ActorFactory.From(_user);
        var now = _clock.GetUtcNow().UtcDateTime;
        switch (Enum.Parse<AttributionOperation>(request.Operation, true))
        {
            case AttributionOperation.ExtendDeadline:
                attribution.ExtendDeadline(request.Deadline!.Value, actor, now);
                break;
            case AttributionOperation.EditDescription:
                attribution.EditDescription(request.Description!, actor, now);
                break;
            case AttributionOperation.Close:
                attribution.Close(actor, now);
                break;
            case AttributionOperation.Deactivate:
                attribution.Deactivate(actor, now);
                break;
            case AttributionOperation.Delete:
                attribution.Delete(actor, now);
                break;
        }

        return Result.Success();
    }
}
