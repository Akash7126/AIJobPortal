using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;
using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Connections;

public sealed record ConfigureGovernmentSourceConnectionCommand(SourceSystem Source, string Endpoint, string AuthMethod, string CredentialRef, bool Enabled)
    : AdminCommand<GovernmentSourceConnectionView>;
