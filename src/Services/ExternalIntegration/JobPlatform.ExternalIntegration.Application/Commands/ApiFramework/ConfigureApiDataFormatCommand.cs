using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

public sealed record ConfigureApiDataFormatCommand(string Version, IReadOnlyList<string> Formats) : AdminCommand<Unit>;
