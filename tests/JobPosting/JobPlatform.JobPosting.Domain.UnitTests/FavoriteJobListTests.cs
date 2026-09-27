using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain.UnitTests;

public class FavoriteJobListTests
{
    [Fact]
    [Trait("Story", "US-3.2.2-03")]
    [Trait("AC", "AC-01")]
    public void Toggle_Add_RaisesEventAndReturnsTrue()
    {
        var owner = Guid.NewGuid();
        var list = FavoriteJobList.CreateEmpty(owner);
        var jobId = Guid.NewGuid();

        var favorited = list.Toggle(jobId, new Actor(owner, false, false), TestKit.At);

        favorited.Should().BeTrue();
        list.JobPostingIds.Should().ContainSingle().Which.Should().Be(jobId);
        list.DomainEvents.Single().Should().BeOfType<FavoriteJobListCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.2.2-03")]
    [Trait("AC", "AC-02")]
    public void Toggle_Twice_RemovesWithoutDuplicating_AndRaisesNoEventOnRemoval()
    {
        var owner = Guid.NewGuid();
        var list = FavoriteJobList.CreateEmpty(owner);
        var jobId = Guid.NewGuid();
        var actor = new Actor(owner, false, false);
        list.Toggle(jobId, actor, TestKit.At);
        list.ClearDomainEvents();

        var favorited = list.Toggle(jobId, actor, TestKit.At);

        favorited.Should().BeFalse();
        list.JobPostingIds.Should().BeEmpty();
        list.DomainEvents.Should().BeEmpty("removal is not published (handover section 3.2)");
    }

    [Fact]
    public void Toggle_ByNonOwner_ThrowsForbidden()
    {
        var list = FavoriteJobList.CreateEmpty(Guid.NewGuid());

        var act = () => list.Toggle(Guid.NewGuid(), new Actor(Guid.NewGuid(), false, false), TestKit.At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.FavoriteForbidden);
    }
}
