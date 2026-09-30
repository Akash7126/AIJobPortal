using JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;
using JobPlatform.ExternalIntegration.Application.DTOs.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.JobDataFlows;

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
