using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application;

internal static class Views
{
    public static IntegrationView ToView(ExternalJobSiteIntegration i) => new(
        i.Id, i.PartnerAccountId, i.SourcePlatform.Id, i.SourcePlatform.Name, i.SourcePlatform.BaseUrl, i.AdmissionStatus.ToString(),
        i.Recommendation.ToString(), i.Status.ToString(), i.Models.PullEnabled, i.Models.PushEnabled, i.Schedule.Mode.ToString(), i.Schedule.Cron,
        i.AttributionVisibility.ToString(), i.Sandbox.ToString(), i.SyncRuns.Select(ToView).ToArray(), i.RowVersion);

    public static SyncRunView ToView(SyncRun r) => new(
        r.Id, r.Trigger.ToString(), r.Status.ToString(), r.StartedAtUtc, r.EndedAtUtc, r.MappingVersion, r.Received, r.Accepted, r.Rejected,
        r.ErrorCode);
}
