using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.CandidateSourcing.Domain.UnitTests;

public class TalentPoolEntryTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Employer = Guid.NewGuid();
    private static readonly Guid Candidate = Guid.NewGuid();
    private static readonly Guid Posting = Guid.NewGuid();

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-01")]
    public void Create_WithVisibleCandidate_Succeeds()
    {
        var entry = TalentPoolEntry.Create(Employer, Candidate, Posting, "note", At, Employer, candidateVisible: true);

        entry.EmployerAccountId.Should().Be(Employer);
        entry.CandidateProfileId.Should().Be(Candidate);
        entry.JobPostingId.Should().Be(Posting);
        entry.Note.Should().Be("note");
        entry.Removed.Should().BeFalse();
        entry.DomainEvents.Single().Should().BeOfType<TalentPoolEntryCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-04")]
    public void Create_WithCandidateNotVisible_Throws()
    {
        var act = () => TalentPoolEntry.Create(Employer, Candidate, Posting, null, At, Employer, candidateVisible: false);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TalentPoolNotVisible);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-02")]
    public void Remove_ByOwner_MarksRemoved()
    {
        var entry = TalentPoolEntry.Create(Employer, Candidate, Posting, null, At, Employer, candidateVisible: true);

        entry.Remove(Employer);

        entry.Removed.Should().BeTrue();
    }

    [Fact]
    public void Remove_ByAnotherEmployer_ThrowsForbidden()
    {
        var entry = TalentPoolEntry.Create(Employer, Candidate, Posting, null, At, Employer, candidateVisible: true);

        var act = () => entry.Remove(Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.Forbidden);
    }

    [Fact]
    public void UpdateNote_ByAnotherEmployer_ThrowsForbidden()
    {
        var entry = TalentPoolEntry.Create(Employer, Candidate, Posting, null, At, Employer, candidateVisible: true);

        var act = () => entry.UpdateNote("x", Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.NotOwner);
    }
}
