using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain.UnitTests;

public class JobPostingLifecycleTests
{
    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-01")]
    public void CreateDraft_StartsInDraft_AndRaisesJobPostingCreated()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Draft(employerId);

        posting.Status.Should().Be(JobPostingStatus.Draft);
        var e = posting.DomainEvents.Single().Should().BeOfType<JobPostingCreatedDomainEvent>().Which;
        e.EmployerAccountId.Should().Be(employerId);
        e.Status.Should().Be(JobPostingStatus.Draft);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-03")]
    public void CreateDraft_WithoutSkills_ThrowsRequiredField()
    {
        var act = () => JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(Guid.NewGuid(), TestKit.Fields(skills: Array.Empty<string>()),
            TestKit.Employer(Guid.NewGuid()), 1, "hash", TestKit.At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.PostingRequiredField);
        ex.ExternalCode.Should().Be(ErrorCodes.RequiredField);
    }

    [Fact]
    [Trait("Story", "US-3.2.4-01")]
    [Trait("AC", "AC-01")]
    public void Publish_FromDraft_MovesToActive_AndRaisesStatusUpdated()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Draft(employerId);
        posting.ClearDomainEvents();

        posting.Publish(TestKit.Employer(employerId), TestKit.At);

        posting.Status.Should().Be(JobPostingStatus.Active);
        posting.PublishedAtUtc.Should().Be(TestKit.At);
        var e = posting.DomainEvents.Single().Should().BeOfType<JobPostingStatusUpdatedDomainEvent>().Which;
        (e.FromStatus, e.ToStatus).Should().Be((JobPostingStatus.Draft, JobPostingStatus.Active));
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-02")]
    public void Publish_WithPastDeadline_ThrowsDeadlineInPast()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Draft(employerId, deadline: TestKit.At.AddDays(30));
        // Simulate time passing beyond the deadline before publish is attempted.
        var afterDeadline = TestKit.At.AddDays(31);

        var act = () => posting.Publish(TestKit.Employer(employerId), afterDeadline);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.PostingDeadlineInPast);
    }

    [Fact]
    public void Publish_ByNonOwner_ThrowsForbidden()
    {
        var posting = TestKit.Draft(Guid.NewGuid());

        var act = () => posting.Publish(TestKit.Employer(Guid.NewGuid()), TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.StatusForbidden);
    }

    [Fact]
    public void Pause_FromActive_MovesToPaused()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Active(employerId);
        posting.ClearDomainEvents();

        posting.Pause(TestKit.Employer(employerId), TestKit.At);

        posting.Status.Should().Be(JobPostingStatus.Paused);
    }

    [Fact]
    public void Pause_FromDraft_ThrowsInvalidTransition()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Draft(employerId);

        var act = () => posting.Pause(TestKit.Employer(employerId), TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.InvalidTransition);
    }

    [Fact]
    public void Resume_FromPaused_MovesToActive()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Active(employerId);
        posting.Pause(TestKit.Employer(employerId), TestKit.At);

        posting.Resume(TestKit.Employer(employerId), TestKit.At);

        posting.Status.Should().Be(JobPostingStatus.Active);
    }

    [Fact]
    [Trait("Story", "US-3.2.4-01")]
    [Trait("AC", "AC-05")]
    public void Resume_WhenAdminSuspended_ThrowsAdminSuspended()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Active(employerId);
        posting.Pause(TestKit.Employer(employerId), TestKit.At);
        posting.ApplyAdminSuspension("policy violation", TestKit.At);

        var act = () => posting.Resume(TestKit.Employer(employerId), TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.AdminSuspended);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-02")]
    public void Expire_FromActive_MovesToExpired_ActorMayBeSystem()
    {
        var posting = TestKit.Active();
        posting.ClearDomainEvents();

        posting.Expire(TestKit.System(), TestKit.At);

        posting.Status.Should().Be(JobPostingStatus.Expired);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-04")]
    [Trait("AC", "AC-01")]
    public void Renew_FromExpired_MovesToActive_WithNewDeadline_AndRaisesBothEvents()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Active(employerId);
        posting.Expire(TestKit.System(), TestKit.At);
        posting.ClearDomainEvents();
        var newDeadline = ApplicationDeadline.Create(TestKit.At.AddDays(60), true);

        posting.Renew(newDeadline, TestKit.Employer(employerId), TestKit.At);

        posting.Status.Should().Be(JobPostingStatus.Active);
        posting.Deadline.DateUtc.Should().Be(newDeadline.DateUtc);
        posting.DomainEvents.Should().HaveCount(2);
        posting.DomainEvents.OfType<JobPostingRenewedDomainEvent>().Should().ContainSingle();
        posting.DomainEvents.OfType<JobPostingStatusUpdatedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.2.1-04")]
    [Trait("AC", "AC-02")]
    public void Renew_WhenNotExpired_ThrowsStateActive()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Active(employerId);

        var act = () => posting.Renew(ApplicationDeadline.Create(TestKit.At.AddDays(60), true), TestKit.Employer(employerId), TestKit.At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.PostingStateActive);
        ex.ExternalCode.Should().Be(ErrorCodes.StateActive);
    }

    [Fact]
    [Trait("Story", "US-3.2.4-01")]
    [Trait("AC", "AC-03")]
    public void Archive_IsTerminal_AnyFurtherTransitionThrowsStateArchived()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Active(employerId);
        posting.Archive(TestKit.Employer(employerId), TestKit.At);

        posting.Status.Should().Be(JobPostingStatus.Archived);
        var act = () => posting.Pause(TestKit.Employer(employerId), TestKit.At);
        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.StateArchived);
    }

    [Theory]
    [InlineData(JobPostingStatus.Draft, JobPostingStatus.Archived, true)]
    [InlineData(JobPostingStatus.Active, JobPostingStatus.Archived, true)]
    [InlineData(JobPostingStatus.Paused, JobPostingStatus.Archived, true)]
    [InlineData(JobPostingStatus.Expired, JobPostingStatus.Archived, true)]
    [InlineData(JobPostingStatus.Draft, JobPostingStatus.Paused, false)]
    [InlineData(JobPostingStatus.Active, JobPostingStatus.Draft, false)]
    public void ChangeStatus_TransitionMatrix(JobPostingStatus from, JobPostingStatus to, bool allowed)
    {
        var employerId = Guid.NewGuid();
        var posting = PostingIn(from, employerId);

        var act = () => posting.ChangeStatus(to, TestKit.Employer(employerId), TestKit.At);

        if (allowed)
        {
            act.Should().NotThrow();
            posting.Status.Should().Be(to);
        }
        else
        {
            act.Should().Throw<BusinessRuleViolationException>();
        }
    }

    [Fact]
    [Trait("Story", "US-3.2.4-01")]
    [Trait("AC", "AC-04")]
    public void ApplyAdminSuspension_ForcesPaused_AndIsIdempotent()
    {
        var posting = TestKit.Active();

        posting.ApplyAdminSuspension("policy violation", TestKit.At);
        var afterFirst = posting.Status;
        posting.ClearDomainEvents();
        posting.ApplyAdminSuspension("policy violation", TestKit.At);

        afterFirst.Should().Be(JobPostingStatus.Paused);
        posting.AdminSuspended.Should().BeTrue();
        posting.DomainEvents.Should().BeEmpty("a second suspension of an already-suspended posting is a no-op");
    }

    private static JobPlatform.JobPosting.Domain.JobPosting PostingIn(JobPostingStatus status, Guid employerId)
    {
        var posting = TestKit.Draft(employerId);
        switch (status)
        {
            case JobPostingStatus.Draft:
                return posting;
            case JobPostingStatus.Active:
                posting.Publish(TestKit.Employer(employerId), TestKit.At);
                return posting;
            case JobPostingStatus.Paused:
                posting.Publish(TestKit.Employer(employerId), TestKit.At);
                posting.Pause(TestKit.Employer(employerId), TestKit.At);
                return posting;
            case JobPostingStatus.Expired:
                posting.Publish(TestKit.Employer(employerId), TestKit.At);
                posting.Expire(TestKit.Employer(employerId), TestKit.At);
                return posting;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
