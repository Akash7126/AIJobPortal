using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

/// <param name="Level">Registration level 1 (company identity) or 2; Level 2 details themselves belong to BC-05.</param>
public sealed record RegisterEmployerAccountCommand(string CompanyName, string Email, string Mobile, string CompanyId, string RegistrationNumber,
    string Password, int Level, string? IdempotencyKey) : ICommand<RegisteredAccountDto>, IIdempotentCommand, IRateLimitedRequest, IConflictAwareCommand
{
    public string RateLimitScope => "registration:employer";
    public string RateLimitedErrorCode => ErrorCodes.EmployerRateLimited;
    public string UniqueViolationErrorCode => ErrorCodes.EmployerDuplicate;
}
