using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.AccountIdentity.Domain.Interfaces.Services;

/// <summary>Duplicate detection across mobile/e-mail/company id/identity (INV-01). Unique indexes remain the final guard.</summary>
public interface IAccountUniquenessChecker
{
    Task<bool> IsMobileTakenAsync(ActorType actorType, MobileNumber mobile, CancellationToken ct = default);

    Task<bool> IsEmailTakenAsync(ActorType actorType, Email email, CancellationToken ct = default);

    Task<bool> IsIdentityKeyTakenAsync(ActorType actorType, ExternalIdentityKey key, CancellationToken ct = default);
}
