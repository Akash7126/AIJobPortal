using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record RegisterJobSeekerAccountCommand(string FullName, string Mobile, string? Email, string Password, string? PreferredLanguage,
    string? IdempotencyKey) : ICommand<RegisteredAccountDto>, IIdempotentCommand, IRateLimitedRequest, IConflictAwareCommand
{
    public string RateLimitScope => "registration:job-seeker";
    public string RateLimitedErrorCode => ErrorCodes.JobSeekerRateLimited;
    public string UniqueViolationErrorCode => ErrorCodes.JobSeekerDuplicate;
}
