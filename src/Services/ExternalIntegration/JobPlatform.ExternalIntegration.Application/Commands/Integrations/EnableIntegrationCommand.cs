using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

public sealed record EnableIntegrationCommand(bool PullEnabled, bool PushEnabled) : PartnerCommand<Unit>;
