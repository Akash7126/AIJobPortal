using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.UnitTests;

public class HelpHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    public async Task CreateHelpContentHandler_CreatesAtVersionOne()
    {
        var store = new FakeStore();
        var handler = new CreateHelpContentHandler(store, Kit.User(), Kit.Clock());

        var result = await handler.Handle(new CreateHelpContentCommand(HelpKind.Faq, null, "Q", null, "A"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentVersion.Should().Be(1);
        store.HelpContents.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-05")]
    [Trait("AC", "AC-01")]
    public async Task UpdateHelpContentHandler_CreatesNewVersion()
    {
        var store = new FakeStore();
        var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q1"), new(null, "A1"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.HelpContents.Add(content);
        var handler = new UpdateHelpContentHandler(store, store, Kit.User(), Kit.Clock());

        var result = await handler.Handle(new UpdateHelpContentCommand(content.Id, null, "Q2", null, "A2"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentVersion.Should().Be(2);
    }

    [Fact]
    public async Task UpdateHelpContentHandler_UnknownId_ReturnsNotFound()
    {
        var handler = new UpdateHelpContentHandler(new FakeStore(), new FakeStore(), Kit.User(), Kit.Clock());

        var result = await handler.Handle(new UpdateHelpContentCommand(Guid.NewGuid(), null, "Q", null, "A"), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-08")]
    [Trait("AC", "AC-02")]
    public async Task AttachHelpMediaHandler_WithoutCaptions_ThrowsBusinessRuleViolation()
    {
        // The domain rule itself throws (as in every BC in this repo); only the full pipeline's UnitOfWorkBehavior maps that to a Result -
        // exercised end to end by AttachHelpMedia_WithoutCaptions_Is415 in the Api integration tests. This unit test covers the FluentValidation
        // guard (AttachHelpMediaValidator, see ValidatorTests) plus the domain invariant itself (HelpContentTests.AttachMedia_*).
        var store = new FakeStore();
        var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Video, new(null, "T"), new(null, "B"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.HelpContents.Add(content);
        var handler = new AttachHelpMediaHandler(store, new FakeMediaStorage(), Kit.User());

        var act = () => handler.Handle(new AttachHelpMediaCommand(content.Id, HelpMediaType.Video, "v.mp4", "video/mp4", 100, new byte[100], null, null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<JobPlatform.SharedKernel.Domain.BusinessRuleViolationException>();
        ex.Which.ExternalCode.Should().Be(Domain.Common.ErrorCodes.HelpUnsupportedFormat);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-02")]
    public async Task UpdateHelpContentOrganizationHandler_CreatesOrganizationWhenMissing()
    {
        var store = new FakeStore();
        var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q"), new(null, "A"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.HelpContents.Add(content);
        var handler = new UpdateHelpContentOrganizationHandler(store, store, Kit.User());
        var topicId = Guid.NewGuid();

        var result = await handler.Handle(new UpdateHelpContentOrganizationCommand(content.Id, topicId, new[] { HelpRole.JobSeeker }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Organizations.Should().ContainSingle().Which.TopicId.Should().Be(topicId);
    }

    [Fact]
    [Trait("Story", "US-3.7.2-04")]
    [Trait("AC", "AC-02")]
    public async Task GetContextHelpHandler_UnmappedPageKey_ReturnsNull()
    {
        var handler = new GetContextHelpHandler(new FakeStore(), new FakeReadStore());

        var result = await handler.Handle(new GetContextHelpQuery("unknown/page"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.7.2-04")]
    [Trait("AC", "AC-01")]
    public async Task SetContextHelpMappingHandler_CreatesThenRepoints()
    {
        var store = new FakeStore();
        var content = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q"), new(null, "A"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        var content2 = Domain.HelpContent.Create(Guid.NewGuid(), HelpKind.Faq, new(null, "Q2"), new(null, "A2"), new(Guid.NewGuid(), true), Kit.Clock().GetUtcNow().UtcDateTime);
        store.HelpContents.AddRange(new[] { content, content2 });
        var handler = new SetContextHelpMappingHandler(store, store, Kit.User());

        await handler.Handle(new SetContextHelpMappingCommand("employer/dashboard", content.Id), CancellationToken.None);
        var second = await handler.Handle(new SetContextHelpMappingCommand("employer/dashboard", content2.Id), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        store.ContextHelpMappings.Should().ContainSingle().Which.HelpContentId.Should().Be(content2.Id);
    }
}
