using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Commands.Authentication;

public sealed record VerifyEmailCommand(Guid AccountId, string Token) : ICommand<Unit>, IRateLimitedRequest
{
    public string RateLimitScope => "email-verification";
    public string RateLimitedErrorCode => ErrorCodes.AuthRateLimited;
}
