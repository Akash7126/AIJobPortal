using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AccountIdentity.Application.Commands.Accounts;

public sealed record RegisterPartnerAccountCommand(string OrganisationName, string ContactEmail, string Mobile, string Identity, string Password,
    string? IdempotencyKey) : ICommand<RegisteredAccountDto>, IIdempotentCommand, IRateLimitedRequest, IConflictAwareCommand
{
    public string RateLimitScope => "registration:partner";
    public string RateLimitedErrorCode => ErrorCodes.AuthRateLimited;
    public string UniqueViolationErrorCode => ErrorCodes.PartnerDuplicate;
}
