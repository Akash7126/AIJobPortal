using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.GovernmentIntegration.Application.Commands.EmployerVerifications;

/// <summary>US-3.1.2-03: employer submits their claim. Automatic matching runs synchronously in the same handler (deviation, see BC-01.md) -
/// this environment has no message-based internal-command dispatch beyond the outbox/inbox used for cross-BC events.</summary>
public sealed record RequestEmployerVerificationCommand(string RegistrationNumber, string VatNumber, string MobileNumber, string? IdempotencyKey)
    : EmployerCommand<EmployerVerificationView>, IIdempotentCommand;
