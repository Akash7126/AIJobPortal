namespace JobPlatform.ExternalIntegration.Application.DTOs.JobDataFlows;

public sealed record PushJobDataResultView(string PlatformJobId, bool Created, string Confirmation);
