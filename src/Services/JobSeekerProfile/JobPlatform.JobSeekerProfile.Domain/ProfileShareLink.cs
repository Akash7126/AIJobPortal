using System.Security.Cryptography;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

/// <summary>AGG-04 ProfileShareLink (handover section 3.3): exists only while sharing is activated; generation is idempotent (AC-03).</summary>
public sealed class ProfileShareLink : AggregateRoot<Guid>
{
    private ProfileShareLink()
    {
    }

    public Guid ProfileId { get; private set; }
    public Guid OwnerAccountId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>INV-10: only when public sharing is activated (checked by the caller against PrivacySetting).</summary>
    public static ProfileShareLink Generate(Guid id, Guid profileId, Guid ownerAccountId, bool sharingActivated, DateTime nowUtc)
    {
        Check(new BusinessRule(RuleCodes.SharingNotActivated, "Activate public sharing before generating a share link.", !sharingActivated,
            ErrorCodes.SharingNotActivated, BusinessRuleKind.BusinessRule));

        return new ProfileShareLink
        {
            Id = id,
            ProfileId = profileId,
            OwnerAccountId = ownerAccountId,
            Token = GenerateToken(),
            IsActive = true,
            CreatedAtUtc = nowUtc
        };
    }

    public void EnsureOwnedBy(Actor actor) => Check(Rules.NotOwner(actor, OwnerAccountId));

    public void Deactivate(Actor actor)
    {
        EnsureOwnedBy(actor);
        IsActive = false;
    }

    private static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
