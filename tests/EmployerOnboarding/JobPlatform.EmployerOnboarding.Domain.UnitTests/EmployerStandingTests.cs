using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.EmployerOnboarding.Domain.UnitTests;

public class EmployerStandingTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OpenFor_StartsUnapprovedAndUnverified()
    {
        var standing = EmployerStanding.OpenFor(Guid.NewGuid(), Guid.NewGuid());

        standing.AdmissionApproved.Should().BeFalse();
        standing.IsVerified.Should().BeFalse();
        standing.BadgeLabel(Language.En).Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-04")]
    [Trait("AC", "AC-01")]
    public void MarkVerified_FirstTime_SetsFlagAndAudit()
    {
        var standing = EmployerStanding.OpenFor(Guid.NewGuid(), Guid.NewGuid());
        var messageId = Guid.NewGuid();

        standing.MarkVerified(messageId, 1, At);

        standing.IsVerified.Should().BeTrue();
        standing.VerifiedAtUtc.Should().Be(At);
        standing.BadgeLabel(Language.En).Should().Be("Verified Employer");
        standing.BadgeLabel(Language.Ar).Should().NotBeNullOrEmpty();
        standing.BadgeAudit.Should().ContainSingle().Which.CausedByMessageId.Should().Be(messageId);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-04")]
    [Trait("AC", "AC-04")]
    public void MarkVerified_DuplicateOrStaleVersion_IsIgnored()
    {
        var standing = EmployerStanding.OpenFor(Guid.NewGuid(), Guid.NewGuid());
        standing.MarkVerified(Guid.NewGuid(), 3, At);

        standing.MarkVerified(Guid.NewGuid(), 3, At.AddMinutes(1));
        standing.MarkVerified(Guid.NewGuid(), 1, At.AddMinutes(2));

        standing.BadgeAudit.Should().ContainSingle("a duplicate or older/out-of-order event must not add a second audit row");
        standing.LastVerificationEventVersion.Should().Be(3);
    }

    [Fact]
    public void MarkAdmissionApproved_IsIdempotent()
    {
        var standing = EmployerStanding.OpenFor(Guid.NewGuid(), Guid.NewGuid());

        standing.MarkAdmissionApproved(At);
        standing.MarkAdmissionApproved(At.AddMinutes(1));

        standing.AdmissionApproved.Should().BeTrue();
        standing.DomainEvents.Should().ContainSingle();
    }
}
