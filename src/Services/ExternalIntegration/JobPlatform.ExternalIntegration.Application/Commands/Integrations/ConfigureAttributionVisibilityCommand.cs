using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

public sealed record ConfigureAttributionVisibilityCommand(string Visibility) : PartnerCommand<Unit>;
