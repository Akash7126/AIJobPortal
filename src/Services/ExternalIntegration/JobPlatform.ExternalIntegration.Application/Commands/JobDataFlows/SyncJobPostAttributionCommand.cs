using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;

public sealed record SyncJobPostAttributionCommand(string PlatformJobId, string Operation, DateTime? Deadline, string? Description)
    : PartnerCommand<Unit>;
