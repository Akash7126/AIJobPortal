using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

/// <summary>Minimum deprecation window (proposed, handover section 7.3): 90 days.</summary>
public sealed record DeprecateApiVersionCommand(string Version, DateTime SunsetAtUtc) : AdminCommand<Unit>;
