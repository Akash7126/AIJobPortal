using System.Text.Json;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Application;

/// <summary>The partner feed did not respond within the retry budget (handover 4.2: 30s timeout × 3 retries). Not a BusinessRuleViolationException
/// because it maps to 502 (ErrorType.External), which the shared rule-to-error mapping does not cover.</summary>
public sealed class PartnerUpstreamTimeoutException : Exception
{
    public PartnerUpstreamTimeoutException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>Application service (handover 4.2): for the pull model, pages the partner feed and runs Receive → Standardize → Accept for each
/// job, independently (a partial failure of one job does not fail the run). For the push model, the same three steps run inline from
/// <see cref="PushJobDataCommand"/> instead of through here.</summary>
public sealed class PartnerSyncOrchestrator
{
    private readonly IJobDataRepository _jobData;
    private readonly IJobDataMappingRepository _mappings;
    private readonly IJobPostAttributionRepository _attributions;
    private readonly IPartnerJobFeedClient _feed;
    private readonly TimeProvider _clock;

    public PartnerSyncOrchestrator(IJobDataRepository jobData, IJobDataMappingRepository mappings, IJobPostAttributionRepository attributions,
        IPartnerJobFeedClient feed, TimeProvider clock)
    {
        _jobData = jobData;
        _mappings = mappings;
        _attributions = attributions;
        _feed = feed;
        _clock = clock;
    }

    public async Task<(int Received, int Accepted, int Rejected)> RunAsync(ExternalJobSiteIntegration integration, SyncRun run, Guid actorId,
        CancellationToken ct)
    {
        IReadOnlyList<PartnerJobPayload> payloads;
        try
        {
            payloads = await _feed.FetchAsync(integration.SourcePlatform.Id, integration.SourcePlatform.BaseUrl, ct);
        }
        catch (TimeoutException ex)
        {
            throw new PartnerUpstreamTimeoutException("The partner feed did not respond in time.", ex);
        }

        var mapping = await _mappings.GetByIntegrationAsync(integration.Id, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        var accepted = 0;
        var rejected = 0;
        foreach (var payload in payloads)
        {
            try
            {
                await AcceptOneAsync(integration, run.Id, mapping, payload, actorId, now, ct);
                accepted++;
            }
            catch (BusinessRuleViolationException)
            {
                rejected++;
            }
        }

        return (payloads.Count, accepted, rejected);
    }

    private async Task AcceptOneAsync(ExternalJobSiteIntegration integration, Guid runId, JobDataMapping? mapping, PartnerJobPayload payload,
        Guid actorId, DateTime now, CancellationToken ct)
    {
        if (mapping is null)
        {
            throw new BusinessRuleViolationException(RuleCodes.MappingRequiredFieldUnmapped, "No mapping is configured for this integration.",
                ErrorCodes.MappingRequiredFieldUnmapped, BusinessRuleKind.BusinessRule);
        }

        var raw = JsonSerializer.Serialize(payload.Fields);
        var existing = await _jobData.GetBySourceKeyAsync(integration.SourcePlatform.Id, payload.SourceJobId, ct);
        JobData jobData;
        if (existing is null)
        {
            jobData = JobData.Receive(Guid.NewGuid(), integration.Id, integration.SourcePlatform.Id, payload.SourceJobId, raw, JobDataModel.Pull, now,
                runId);
            _jobData.Add(jobData);
        }
        else
        {
            jobData = existing;
            jobData.UpdateRaw(raw, runId, now);
        }

        var standardJob = mapping.Standardize(payload.Fields);
        jobData.Standardize(standardJob);
        var wasNew = jobData.PlatformJobId is null;
        jobData.Accept(actorId, integration.AttributionVisibility.ToString(), now);
        if (wasNew)
        {
            _attributions.Add(JobPostAttribution.Tag(Guid.NewGuid(), jobData.Id, jobData.PlatformJobId!, integration.SourcePlatform.Name,
                standardJob.SourceUrl, actorId, now));
        }
    }
}
