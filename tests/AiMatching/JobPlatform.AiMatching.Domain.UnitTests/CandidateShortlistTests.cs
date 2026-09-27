using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class CandidateShortlistTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly MatchingConfigSnapshot Config = MatchingConfigSnapshot.Defaults;

    [Fact]
    public void Request_ByOwningEmployer_CreatesQueuedShortlist()
    {
        var employer = Guid.NewGuid();
        var actor = new Actor(employer, ActorType.Employer);

        var shortlist = CandidateShortlist.Request(Guid.NewGuid(), employer, actor, 100, Config, At);

        shortlist.Status.Should().Be(ShortlistStatus.Queued);
        shortlist.RequestedSize.Should().Be(100);
        shortlist.ConfigVersion.Should().Be(Config.ConfigVersion);
    }

    [Fact]
    public void Request_ByNonOwner_ThrowsForbidden()
    {
        var owner = Guid.NewGuid();
        var stranger = new Actor(Guid.NewGuid(), ActorType.Employer);

        var act = () => CandidateShortlist.Request(Guid.NewGuid(), owner, stranger, 100, Config, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AiRuleCodes.ShortlistNotOwner);
        ex.ExternalCode.Should().Be(AiErrorCodes.Forbidden);
    }

    [Fact]
    public void Request_ByNonEmployerActor_ThrowsForbiddenEvenWithMatchingId()
    {
        var owner = Guid.NewGuid();
        var wrongType = new Actor(owner, ActorType.JobSeeker);

        var act = () => CandidateShortlist.Request(Guid.NewGuid(), owner, wrongType, 100, Config, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.ShortlistNotOwner);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Request_SizeOutOfBounds_Throws(int size)
    {
        var employer = Guid.NewGuid();
        var actor = new Actor(employer, ActorType.Employer);

        var act = () => CandidateShortlist.Request(Guid.NewGuid(), employer, actor, size, Config, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.InvalidInput);
    }

    private static CandidateShortlist Queued(int size = 10)
    {
        var employer = Guid.NewGuid();
        return CandidateShortlist.Request(Guid.NewGuid(), employer, new Actor(employer, ActorType.Employer), size, Config, At);
    }

    [Fact]
    public void Complete_FromQueued_RanksAndMarksReady()
    {
        var shortlist = Queued(size: 2);
        var candidates = new[] { (Guid.NewGuid(), 50m), (Guid.NewGuid(), 90m), (Guid.NewGuid(), 70m) };

        shortlist.Complete(candidates, At.AddMinutes(5));

        shortlist.Status.Should().Be(ShortlistStatus.Ready);
        shortlist.Items.Should().HaveCount(2);
        shortlist.Items[0].Score.Should().Be(90);
        shortlist.Items[0].Rank.Should().Be(1);
    }

    [Fact]
    public void Complete_FewerCandidatesThanRequestedSize_ReturnsAllWithoutPadding()
    {
        var shortlist = Queued(size: 100);

        shortlist.Complete(new[] { (Guid.NewGuid(), 50m) }, At);

        shortlist.Items.Should().ContainSingle();
    }

    [Fact]
    public void Complete_WhenNotQueued_ThrowsConflict()
    {
        var shortlist = Queued();
        shortlist.Complete(Array.Empty<(Guid, decimal)>(), At);

        var act = () => shortlist.Complete(Array.Empty<(Guid, decimal)>(), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    public void Fail_FromQueued_SetsFailedAndTruncatesLongReason()
    {
        var shortlist = Queued();

        shortlist.Fail(new string('x', 600), At);

        shortlist.Status.Should().Be(ShortlistStatus.Failed);
        shortlist.FailureReason.Should().HaveLength(500);
    }

    [Fact]
    public void Fail_WhenNotQueued_ThrowsConflict()
    {
        var shortlist = Queued();
        shortlist.Fail("boom", At);

        var act = () => shortlist.Fail("again", At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    public void Rank_TiesBrokenByLowestProfileId()
    {
        var lower = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higher = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var ranked = CandidateShortlist.Rank(new[] { (higher, 80m), (lower, 80m) }, 10);

        ranked[0].ProfileId.Should().Be(lower);
        ranked[1].ProfileId.Should().Be(higher);
    }
}
