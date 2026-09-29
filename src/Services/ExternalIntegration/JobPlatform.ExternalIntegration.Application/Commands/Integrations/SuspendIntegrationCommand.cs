using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

public sealed record SuspendIntegrationCommand(Guid IntegrationId, string Reason) : AdminCommand<Unit>;
