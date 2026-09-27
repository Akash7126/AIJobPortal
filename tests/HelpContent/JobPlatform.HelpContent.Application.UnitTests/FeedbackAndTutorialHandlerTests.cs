using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.UnitTests;

public class FeedbackHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-01")]
    public async Task SubmitHelpFeedbackHandler_New_AddsFeedback()
    {
        var store = new FakeStore();
        var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q"), new(null, "A"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.HelpContents.Add(content);
        var handler = new SubmitHelpFeedbackHandler(store, store, Kit.User(SharedKernel.Common.Enums.ActorType.JobSeeker), Kit.Clock());

        var result = await handler.Handle(new SubmitHelpFeedbackCommand(content.Id, FeedbackRating.Helpful, "Great"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Feedback.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-02")]
    public async Task SubmitHelpFeedbackHandler_SameUserTwice_ReplacesInsteadOfDuplicating()
    {
        var store = new FakeStore();
        var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q"), new(null, "A"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.HelpContents.Add(content);
        var user = Kit.User(SharedKernel.Common.Enums.ActorType.JobSeeker, Guid.NewGuid());
        var handler = new SubmitHelpFeedbackHandler(store, store, user, Kit.Clock());

        await handler.Handle(new SubmitHelpFeedbackCommand(content.Id, FeedbackRating.Helpful, "First"), CancellationToken.None);
        await handler.Handle(new SubmitHelpFeedbackCommand(content.Id, FeedbackRating.NotHelpful, "Changed"), CancellationToken.None);

        store.Feedback.Should().ContainSingle();
        store.Feedback.Single().Rating.Should().Be(FeedbackRating.NotHelpful);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    public async Task SubmitHelpFeedbackHandler_UnknownContent_ReturnsNotFound()
    {
        var handler = new SubmitHelpFeedbackHandler(new FakeStore(), new FakeStore(), Kit.User(SharedKernel.Common.Enums.ActorType.JobSeeker), Kit.Clock());

        var result = await handler.Handle(new SubmitHelpFeedbackCommand(Guid.NewGuid(), FeedbackRating.Helpful, null), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-03")]
    public async Task GetHelpFeedbackSummaryHandler_ReturnsZeroesWhenNoFeedback()
    {
        var handler = new GetHelpFeedbackSummaryHandler(new FakeReadStore());

        var result = await handler.Handle(new GetHelpFeedbackSummaryQuery(Guid.NewGuid()), CancellationToken.None);

        result.Value.HelpfulCount.Should().Be(0);
        result.Value.NotHelpfulCount.Should().Be(0);
    }
}

public class TutorialHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.7.2-07")]
    [Trait("AC", "AC-01")]
    public async Task GetOnboardingTutorialHandler_NoProgress_NotCompleted()
    {
        var store = new FakeReadStore { HelpContent = _ => new HelpContentView(Guid.NewGuid(), "Guide", new(null, "T"), new(null, "B"), 1, null, Array.Empty<string>(), Array.Empty<HelpMediaView>(), Array.Empty<byte>()) };
        var userId = Guid.NewGuid();
        var handler = new GetOnboardingTutorialHandler(store, new FakeStore(), Kit.User(SharedKernel.Common.Enums.ActorType.JobSeeker, userId));

        var result = await handler.Handle(new GetOnboardingTutorialQuery(Guid.NewGuid()), CancellationToken.None);

        result.Value.Completed.Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-07")]
    [Trait("AC", "AC-02")]
    public async Task CompleteTutorialHandler_ThenGet_ReportsCompleted()
    {
        var tutorialId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = new FakeStore();
        var completeHandler = new CompleteTutorialHandler(store, Kit.User(SharedKernel.Common.Enums.ActorType.JobSeeker, userId), Kit.Clock());
        await completeHandler.Handle(new CompleteTutorialCommand(tutorialId), CancellationToken.None);

        var readStore = new FakeReadStore { HelpContent = _ => new HelpContentView(tutorialId, "Guide", new(null, "T"), new(null, "B"), 1, null, Array.Empty<string>(), Array.Empty<HelpMediaView>(), Array.Empty<byte>()) };
        var getHandler = new GetOnboardingTutorialHandler(readStore, store, Kit.User(SharedKernel.Common.Enums.ActorType.JobSeeker, userId));
        var result = await getHandler.Handle(new GetOnboardingTutorialQuery(tutorialId), CancellationToken.None);

        result.Value.Completed.Should().BeTrue();
        result.Value.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-07")]
    public async Task CompleteTutorialHandler_Twice_DoesNotDuplicate()
    {
        var store = new FakeStore();
        var userId = Guid.NewGuid();
        var tutorialId = Guid.NewGuid();
        var handler = new CompleteTutorialHandler(store, Kit.User(SharedKernel.Common.Enums.ActorType.JobSeeker, userId), Kit.Clock());

        await handler.Handle(new CompleteTutorialCommand(tutorialId), CancellationToken.None);
        await handler.Handle(new CompleteTutorialCommand(tutorialId), CancellationToken.None);

        store.TutorialProgresses.Should().ContainSingle();
    }
}
