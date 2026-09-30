using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

/// <summary>A wrong current password counts as a failed attempt against the lockout, so the aggregate is persisted on failure.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : AuthenticatedRequest, ICommand<Unit>, IPersistOnFailure;
