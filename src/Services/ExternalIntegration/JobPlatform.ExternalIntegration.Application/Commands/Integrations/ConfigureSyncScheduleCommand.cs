using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

public sealed record ConfigureSyncScheduleCommand(string Mode, string? Cron) : PartnerCommand<Unit>;
