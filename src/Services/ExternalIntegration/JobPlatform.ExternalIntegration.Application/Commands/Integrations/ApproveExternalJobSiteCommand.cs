using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.Integrations;

public sealed record ApproveExternalJobSiteCommand(Guid IntegrationId, string ApprovalBasis) : AdminCommand<Unit>;
