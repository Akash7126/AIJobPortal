using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Commands.EmployerVerifications;

/// <summary>US-3.1.2-03 (MoL decision). Handover Q-03 (proposed): reuses the Administrator role for the MoL reviewer, and adds a Reject path.</summary>
public sealed record DecideEmployerVerificationManuallyCommand(Guid EmployerVerificationId, ManualDecision Decision, string? Reason)
    : AdminCommand<Unit>;
