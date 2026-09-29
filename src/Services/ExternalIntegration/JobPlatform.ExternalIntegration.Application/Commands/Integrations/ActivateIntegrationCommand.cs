using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

public sealed record ActivateIntegrationCommand(Guid IntegrationId) : AdminCommand<Unit>;
