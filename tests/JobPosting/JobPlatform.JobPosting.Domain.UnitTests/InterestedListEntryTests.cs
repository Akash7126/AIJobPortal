using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain.UnitTests;

public class InterestedListEntryTests
{
    [Fact]
    [Trait("Story", "US-3.2.3-01")]
    [Trait("AC", "AC-01")]
    public void Add_ToPosting_RaisesInterestedListEntryCreated()
    {
        var owner = Guid.NewGuid();
        var postingId = Guid.NewGuid();

        var entry = InterestedListEntry.Add(owner, InterestedReference.ToPosting(postingId), new Actor(owner, false, false), TestKit.At);

        entry.Reference.Type.Should().Be(InterestedReferenceType.Posting);
        entry.Reference.PostingId.Should().Be(postingId);
        entry.DomainEvents.Single().Should().BeOfType<InterestedListEntryCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.2.3-01")]
    [Trait("AC", "AC-02")]
    public void Matches_SameReference_ReturnsTrue_DifferentReference_ReturnsFalse()
    {
        var owner = Guid.NewGuid();
        var postingId = Guid.NewGuid();
        var entry = InterestedListEntry.Add(owner, InterestedReference.ToPosting(postingId), new Actor(owner, false, false), TestKit.At);

        entry.Matches(InterestedReference.ToPosting(postingId)).Should().BeTrue();
        entry.Matches(InterestedReference.ToPosting(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public void Add_ByNonOwner_ThrowsForbidden()
    {
        var act = () => InterestedListEntry.Add(Guid.NewGuid(), InterestedReference.ToPosting(Guid.NewGuid()), new Actor(Guid.NewGuid(), false, false), TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.InterestedForbidden);
    }
}
