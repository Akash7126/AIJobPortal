using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class EmployerVerificationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Submission ValidSubmission() => new("REG-123", "VAT-456", "+970591234567");

    private static EmployerVerification Requested(Guid? employerId = null) =>
        EmployerVerification.Request(Guid.NewGuid(), employerId ?? Guid.NewGuid(), ValidSubmission());

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-01")]
    public void Request_WithCompleteSubmission_StartsPending()
    {
        var verification = Requested();

        verification.State.Should().Be(VerificationState.Pending);
        verification.Method.Should().Be(VerificationMethod.Automatic);
        verification.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-01")]
    public void Request_WithIncompleteSubmission_ThrowsSubmissionIncomplete()
    {
        var act = () => EmployerVerification.Request(Guid.NewGuid(), Guid.NewGuid(), new Submission("", "VAT", "+970591234567"));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.EmployerVerificationSubmissionIncomplete);
        ex.ExternalCode.Should().Be("E-GI-SUBMISSION-INCOMPLETE");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-01")]
    public void MarkMatched_FromPending_TransitionsToVerifiedAndRaisesEvent()
    {
        var verification = Requested();

        verification.MarkMatched(SourceSystem.MoL, At);

        verification.State.Should().Be(VerificationState.Verified);
        verification.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployerVerificationApprovedDomainEvent>()
            .Which.Method.Should().Be("Automatic");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-02")]
    public void EscalateToManualReview_FromPending_SetsPendingManualReviewAndMoLMethod()
    {
        var verification = Requested();

        verification.EscalateToManualReview("No automatic match found.");

        verification.State.Should().Be(VerificationState.PendingManualReview);
        verification.Method.Should().Be(VerificationMethod.ManualMoL);
        verification.FailureReason.Should().Be("No automatic match found.");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-03")]
    public void RecordAttempt_ThreeTimeouts_AutomaticAttemptsExhausted()
    {
        var verification = Requested();

        for (var i = 0; i < EmployerVerification.MaxAutomaticAttempts; i++)
        {
            verification.RecordAttempt(SourceSystem.MoL, AttemptOutcome.Timeout, "E-ERPM-UPSTREAM-TIMEOUT", At);
        }

        verification.AutomaticAttemptsExhausted.Should().BeTrue();
        verification.Attempts.Should().HaveCount(3);
        verification.Attempts.Select(a => a.AttemptNo).Should().Equal(1, 2, 3);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-01")]
    public void ApproveManually_FromPendingManualReview_TransitionsToVerifiedWithReviewerActor()
    {
        var verification = Requested();
        verification.EscalateToManualReview("no match");
        var reviewerId = Guid.NewGuid();

        verification.ApproveManually(reviewerId, At);

        verification.State.Should().Be(VerificationState.Verified);
        verification.DecidedBy.Should().Be(reviewerId);
        verification.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployerVerificationApprovedDomainEvent>()
            .Which.ActorId.Should().Be(reviewerId);
    }

    [Fact]
    public void ApproveManually_WhenNotInManualReview_ThrowsNotInManualReview()
    {
        var verification = Requested();

        var act = () => verification.ApproveManually(Guid.NewGuid(), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.EmployerVerificationNotInManualReview);
        ex.ExternalCode.Should().Be("E-GI-NOT-IN-MANUAL-REVIEW");
    }

    [Fact]
    public void RejectManually_FromPendingManualReview_TransitionsToRejected()
    {
        var verification = Requested();
        verification.EscalateToManualReview("no match");
        var reviewerId = Guid.NewGuid();

        verification.RejectManually(reviewerId, "Registration number could not be confirmed.", At);

        verification.State.Should().Be(VerificationState.Rejected);
        verification.FailureReason.Should().Be("Registration number could not be confirmed.");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public void MarkMatched_AfterAlreadyDecided_ThrowsAlreadyDecided()
    {
        var verification = Requested();
        verification.MarkMatched(SourceSystem.MoL, At);

        var act = () => verification.MarkMatched(SourceSystem.MoL, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.EmployerVerificationAlreadyDecided);
        ex.ExternalCode.Should().Be("E-GI-ALREADY-DECIDED");
    }

    [Fact]
    public void Submission_AdditionalFields_DefaultsToEmptyDictionary()
    {
        var submission = new Submission("R", "V", "+970591234567");

        submission.AdditionalFields.Should().BeEmpty();
        submission.IsComplete.Should().BeTrue();
    }
}
