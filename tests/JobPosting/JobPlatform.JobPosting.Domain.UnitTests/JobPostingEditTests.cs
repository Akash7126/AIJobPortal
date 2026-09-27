using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain.UnitTests;

public class JobPostingEditTests
{
    [Fact]
    [Trait("Story", "US-3.2.1-03")]
    [Trait("AC", "AC-01")]
    public void Edit_ByOwner_UpdatesFieldsAndRaisesUpdated_WithChangedFieldList()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Draft(employerId);
        posting.ClearDomainEvents();
        var newFields = TestKit.Fields(skills: new[] { "python" });

        posting.Edit(newFields, TestKit.Employer(employerId), 1, ContentHasher.Hash(newFields), TestKit.At);

        posting.Skills.Should().BeEquivalentTo(new[] { "python" });
        var e = posting.DomainEvents.Single().Should().BeOfType<JobPostingUpdatedDomainEvent>().Which;
        e.ChangedFields.Should().Contain("skills");
    }

    [Fact]
    [Trait("Story", "US-3.2.1-03")]
    [Trait("AC", "AC-05")]
    public void Edit_ByNonOwner_ThrowsForbidden()
    {
        var posting = TestKit.Draft(Guid.NewGuid());

        var act = () => posting.Edit(TestKit.Fields(), TestKit.Employer(Guid.NewGuid()), 1, "h2", TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.PostingForbidden);
    }

    [Fact]
    public void Edit_WithNoActualChange_RaisesNoEvent()
    {
        var employerId = Guid.NewGuid();
        var fields = TestKit.Fields();
        var posting = JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(employerId, fields, TestKit.Employer(employerId), 1, ContentHasher.Hash(fields), TestKit.At);
        posting.ClearDomainEvents();

        posting.Edit(fields, TestKit.Employer(employerId), 1, ContentHasher.Hash(fields), TestKit.At);

        posting.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Edit_OnArchivedPosting_ThrowsStateArchived()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Active(employerId);
        posting.Archive(TestKit.Employer(employerId), TestKit.At);

        var act = () => posting.Edit(TestKit.Fields(skills: new[] { "go" }), TestKit.Employer(employerId), 1, "h2", TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.StateArchived);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-03")]
    [Trait("AC", "AC-02")]
    public void SetVisibility_ToPrivate_UpdatesAndRaisesEvent()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Draft(employerId);
        posting.ClearDomainEvents();

        posting.SetVisibility(JobVisibility.Private(), TestKit.Employer(employerId), TestKit.At);

        posting.Visibility.Scope.Should().Be(VisibilityScope.Private);
        posting.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobPostingUpdatedDomainEvent>();
    }

    [Fact]
    public void ExtendDeadline_WithPastDate_ThrowsDeadlineInPast()
    {
        var employerId = Guid.NewGuid();
        var posting = TestKit.Draft(employerId);

        var act = () => posting.ExtendDeadline(ApplicationDeadline.Create(TestKit.At.AddDays(-1), true), TestKit.Employer(employerId), TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.PostingDeadlineInPast);
    }

    [Fact]
    public void UpdateFromExternal_BySystemActor_BypassesOwnerCheck()
    {
        var source = JobSource.FromExternal(Guid.NewGuid(), "job-1", "Indeed", "https://example.com", true);
        var fields = TestKit.Fields();
        var posting = JobPlatform.JobPosting.Domain.JobPosting.CreateFromExternal(source, fields, 1, ContentHasher.Hash(fields), TestKit.At);
        posting.ClearDomainEvents();

        var updated = TestKit.Fields(skills: new[] { "rust" });
        var act = () => posting.UpdateFromExternal(updated, 1, ContentHasher.Hash(updated), TestKit.At);

        act.Should().NotThrow();
        posting.Skills.Should().BeEquivalentTo(new[] { "rust" });
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-04")]
    public void CreateFromExternal_PublishesDirectlyToActive()
    {
        var source = JobSource.FromExternal(Guid.NewGuid(), "job-2", "Bayt", "https://example.com", true);
        var fields = TestKit.Fields();

        var posting = JobPlatform.JobPosting.Domain.JobPosting.CreateFromExternal(source, fields, 1, ContentHasher.Hash(fields), TestKit.At);

        posting.Status.Should().Be(JobPostingStatus.Active);
        posting.Source.Type.Should().Be(JobSourceType.External);
        posting.DomainEvents.Single().Should().BeOfType<JobPostingCreatedDomainEvent>();
    }
}
