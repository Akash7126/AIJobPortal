using JobPlatform.CandidateSourcing.Application.Commands.TalentPool;
using JobPlatform.CandidateSourcing.Application.Commands.Threshold;
using JobPlatform.CandidateSourcing.Application.Handlers.TalentPool;
using JobPlatform.CandidateSourcing.Application.Handlers.Threshold;
using JobPlatform.CandidateSourcing.Application.Queries.TalentPool;
using JobPlatform.CandidateSourcing.Application.Queries.Threshold;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.CandidateSourcing.Application.UnitTests;

public class TalentPoolAndThresholdHandlerTests
{
    private readonly FakeStore _store = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Kit.Clock();

    private CandidateProjection VisibleCandidate(Guid profileId)
    {
        var projection = CandidateProjection.Create(profileId, Guid.NewGuid());
        projection.ApplyCandidateView(CandidateVisibility.Public, false, false, Array.Empty<string>(), null, null, null, null, null, null, 1,
            _clock.GetUtcNow().UtcDateTime);
        _store.Projections.Add(projection);
        return projection;
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-01")]
    public async Task AddToTalentPool_NewVisibleCandidate_CreatesEntry()
    {
        var candidate = Guid.NewGuid();
        VisibleCandidate(candidate);
        var handler = new AddToTalentPoolHandler(_store, _store, Kit.User(), _clock);

        var result = await handler.Handle(new AddToTalentPoolCommand(candidate, Guid.NewGuid(), "promising"), default);

        result.IsSuccess.Should().BeTrue();
        _store.Entries.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-02")]
    public async Task AddToTalentPool_AlreadySaved_ReturnsExistingWithoutDuplicating()
    {
        var candidate = Guid.NewGuid();
        var posting = Guid.NewGuid();
        VisibleCandidate(candidate);
        var handler = new AddToTalentPoolHandler(_store, _store, Kit.User(), _clock);
        var first = await handler.Handle(new AddToTalentPoolCommand(candidate, posting, null), default);

        var second = await handler.Handle(new AddToTalentPoolCommand(candidate, posting, null), default);

        second.Value.TalentPoolEntryId.Should().Be(first.Value.TalentPoolEntryId);
        _store.Entries.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-07")]
    [Trait("AC", "AC-04")]
    public async Task AddToTalentPool_CandidateNotInProjection_ThrowsNotVisible()
    {
        var handler = new AddToTalentPoolHandler(_store, _store, Kit.User(), _clock);

        var act = () => handler.Handle(new AddToTalentPoolCommand(Guid.NewGuid(), Guid.NewGuid(), null), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.Code.Should().Be(Domain.Common.RuleCodes.TalentPoolNotVisible);
    }

    [Fact]
    public async Task RemoveFromTalentPool_UnknownId_ReturnsNotFound()
    {
        var handler = new RemoveFromTalentPoolHandler(_store, Kit.User());

        var result = await handler.Handle(new RemoveFromTalentPoolCommand(Guid.NewGuid()), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task RemoveFromTalentPool_ByAnotherEmployer_Throws()
    {
        var candidate = Guid.NewGuid();
        VisibleCandidate(candidate);
        var addHandler = new AddToTalentPoolHandler(_store, _store, Kit.User(), _clock);
        var added = await addHandler.Handle(new AddToTalentPoolCommand(candidate, Guid.NewGuid(), null), default);
        var removeHandler = new RemoveFromTalentPoolHandler(_store, Kit.User(id: Guid.NewGuid()));

        var act = () => removeHandler.Handle(new RemoveFromTalentPoolCommand(added.Value.TalentPoolEntryId), default);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task ListTalentPool_ReturnsOnlyTheCallersEntries()
    {
        var candidate = Guid.NewGuid();
        VisibleCandidate(candidate);
        var mine = Kit.User();
        await new AddToTalentPoolHandler(_store, _store, mine, _clock).Handle(new AddToTalentPoolCommand(candidate, Guid.NewGuid(), null), default);
        var other = Guid.NewGuid();
        VisibleCandidate(other);
        await new AddToTalentPoolHandler(_store, _store, Kit.User(id: Guid.NewGuid()), _clock).Handle(new AddToTalentPoolCommand(other, Guid.NewGuid(), null), default);

        var result = await new ListTalentPoolHandler(_store, mine).Handle(new ListTalentPoolQuery(), default);

        result.Value.Should().ContainSingle().Which.CandidateProfileId.Should().Be(candidate);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    [Trait("AC", "AC-01")]
    public async Task SetQualificationThreshold_FirstTime_CreatesThreshold()
    {
        var handler = new SetQualificationThresholdHandler(_store, Kit.User(), _clock);

        var result = await handler.Handle(new SetQualificationThresholdCommand(Guid.NewGuid(), 65), default);

        result.Value.Percent.Should().Be(65);
        _store.Thresholds.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    [Trait("AC", "AC-04")]
    public async Task SetQualificationThreshold_SecondTime_UpdatesSameThreshold()
    {
        var posting = Guid.NewGuid();
        var handler = new SetQualificationThresholdHandler(_store, Kit.User(), _clock);
        await handler.Handle(new SetQualificationThresholdCommand(posting, 40), default);

        var result = await handler.Handle(new SetQualificationThresholdCommand(posting, 80), default);

        result.Value.Percent.Should().Be(80);
        _store.Thresholds.Should().ContainSingle();
    }

    [Fact]
    public async Task GetQualificationThreshold_NoneSet_ReturnsZero()
    {
        var result = await new GetQualificationThresholdHandler(_store, Kit.User()).Handle(new GetQualificationThresholdQuery(Guid.NewGuid()), default);

        result.Value.Percent.Should().Be(0);
    }
}
