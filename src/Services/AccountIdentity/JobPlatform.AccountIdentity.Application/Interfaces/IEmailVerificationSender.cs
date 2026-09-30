using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

public interface IEmailVerificationSender
{
    Task SendVerificationAsync(Email email, Guid accountId, string token, Language language, CancellationToken ct = default);

    Task SendLoginCodeAsync(Email email, string code, Language language, CancellationToken ct = default);
}
