namespace JobPlatform.JobPosting.Domain.UnitTests;

public class SavedSearchTests
{
    private static SearchCriteria Criteria(string category = "software-development") => new(null, "Ramallah", null, null, null, null, null, null, category);

    [Fact]
    [Trait("Story", "US-3.2.2-04")]
    [Trait("AC", "AC-02")]
    public void Hash_OfIdenticalCriteria_IsEqual_CaseInsensitive()
    {
        var a = new SearchCriteria("Developer", "Ramallah", null, 1000, 2000, ContractType.FullTime, null, null, "software-development");
        var b = new SearchCriteria("developer", "ramallah", null, 1000, 2000, ContractType.FullTime, null, null, "SOFTWARE-DEVELOPMENT");

        SavedSearch.Hash(a).Should().Be(SavedSearch.Hash(b));
    }

    [Fact]
    [Trait("Story", "US-3.2.2-04")]
    [Trait("AC", "AC-04")]
    public void EvaluateMatch_ActivePostingMatchingCriteria_RaisesSavedSearchMatched()
    {
        var jobSeeker = Guid.NewGuid();
        var search = SavedSearch.Save(jobSeeker, Criteria(), notifyOnMatch: true, TestKit.At);
        var posting = TestKit.Active();

        var matched = search.EvaluateMatch(posting, TestKit.At);

        matched.Should().BeTrue();
        var e = search.DomainEvents.Single().Should().BeOfType<SavedSearchMatchedDomainEvent>().Which;
        e.SavedSearchId.Should().Be(search.Id);
        e.JobPostingId.Should().Be(posting.Id);
    }

    [Fact]
    public void EvaluateMatch_WhenNotifyOnMatchIsFalse_NeverMatches()
    {
        var search = SavedSearch.Save(Guid.NewGuid(), Criteria(), notifyOnMatch: false, TestKit.At);
        var posting = TestKit.Active();

        var matched = search.EvaluateMatch(posting, TestKit.At);

        matched.Should().BeFalse();
        search.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateMatch_NonActivePosting_NeverMatches()
    {
        var search = SavedSearch.Save(Guid.NewGuid(), Criteria(), notifyOnMatch: true, TestKit.At);
        var posting = TestKit.Draft();

        search.EvaluateMatch(posting, TestKit.At).Should().BeFalse();
    }

    [Fact]
    public void SetNotify_ByNonOwner_ThrowsForbidden()
    {
        var search = SavedSearch.Save(Guid.NewGuid(), Criteria(), notifyOnMatch: false, TestKit.At);

        var act = () => search.SetNotify(true, new Actor(Guid.NewGuid(), false, false));

        act.Should().Throw<SharedKernel.Domain.BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.FavoriteForbidden);
    }
}
