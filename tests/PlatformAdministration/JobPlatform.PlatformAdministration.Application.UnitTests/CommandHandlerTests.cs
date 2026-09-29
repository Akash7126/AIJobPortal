using JobPlatform.PlatformAdministration.Application.Commands.Entities;
using JobPlatform.PlatformAdministration.Application.Commands.Offerings;
using JobPlatform.PlatformAdministration.Application.Commands.Reference;
using JobPlatform.PlatformAdministration.Application.Commands.Settings;
using JobPlatform.PlatformAdministration.Application.Commands.Taxonomy;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;
using JobPlatform.PlatformAdministration.Application.DTOs.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Events;
using JobPlatform.PlatformAdministration.Application.Handlers.Entities;
using JobPlatform.PlatformAdministration.Application.Handlers.Offerings;
using JobPlatform.PlatformAdministration.Application.Handlers.Reference;
using JobPlatform.PlatformAdministration.Application.Handlers.Settings;
using JobPlatform.PlatformAdministration.Application.Handlers.Taxonomy;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JobPlatform.PlatformAdministration.Application.UnitTests;

public class CommandHandlerTests
{
    private readonly FakeStore _store = new();
    private readonly FakeCache _cache = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Kit.Clock();
    private static readonly Dictionary<string, string> Employer = new() { ["companyName"] = "Acme", ["companyId"] = "CO-1" };

    [Fact]
    public async Task CreateEntityRecord_AddsRecordAndReturnsView()
    {
        var handler = new CreatePlatformEntityRecordHandler(_store, Kit.User(), _clock);

        var result = await handler.Handle(new CreatePlatformEntityRecordCommand(PlatformEntityType.Employer, Employer), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IdentityKey.Should().Be("co-1");
        _store.Records.Should().ContainSingle().Which.CreatedBy.Should().Be(Kit.AdminId);
    }

    [Fact]
    public async Task CreateEntityRecord_Twice_SecondThrowsDuplicate()
    {
        var handler = new CreatePlatformEntityRecordHandler(_store, Kit.User(), _clock);
        await handler.Handle(new CreatePlatformEntityRecordCommand(PlatformEntityType.Employer, Employer), default);

        var act = () => handler.Handle(new CreatePlatformEntityRecordCommand(PlatformEntityType.Employer, Employer), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-AUM-DUPLICATE");
    }

    [Fact]
    public async Task CreateEntityRecord_ByNonAdministrator_ThrowsForbidden()
    {
        var handler = new CreatePlatformEntityRecordHandler(_store, Kit.User(ActorType.Employer), _clock);

        var act = () => handler.Handle(new CreatePlatformEntityRecordCommand(PlatformEntityType.Employer, Employer), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }

    [Fact]
    public async Task ChangeSetting_UnknownKeyInStore_ReturnsNotFound()
    {
        var handler = new ChangeSystemSettingHandler(_store, Kit.User(), _clock, NullLogger<ChangeSystemSettingHandler>.Instance);

        var result = await handler.Handle(new ChangeSystemSettingCommand("upload.maxSizeMb", "10"), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ChangeSetting_UpdatesValueAndReturnsNewVersion()
    {
        _store.Settings.Add(SystemSetting.Define(SystemSettingCatalog.Find("upload.maxSizeMb")!, _clock.GetUtcNow().UtcDateTime));
        var handler = new ChangeSystemSettingHandler(_store, Kit.User(), _clock, NullLogger<ChangeSystemSettingHandler>.Instance);

        var result = await handler.Handle(new ChangeSystemSettingCommand("upload.maxSizeMb", "10"), default);

        result.Value.Version.Should().Be(2);
        _store.Settings.Single().Value.Should().Be("10");
    }

    private ReferenceFile SeedFile(params string[] codes)
    {
        var file = ReferenceFile.Create(ReferenceFileType.Skills, _clock.GetUtcNow().UtcDateTime);
        if (codes.Length > 0) file.ApplyChanges(codes.Select(c => new ReferenceChange(ReferenceChangeKind.Add, null, c, new(c, c), null)).ToList(), false, Array.Empty<string>(),
            new Actor(Kit.AdminId, true), _clock.GetUtcNow().UtcDateTime);
        _store.Files.Add(file);
        return file;
    }

    [Fact]
    public async Task UpdateReferenceFile_UnknownFile_ReturnsNotFound()
    {
        var handler = new UpdateReferenceFileHandler(_store, Substitute.For<IReferenceUsageChecker>(), Kit.User(), _clock);

        var result = await handler.Handle(new UpdateReferenceFileCommand("skills", false, new[] { new ReferenceChangeRequest("add", null, "a", new("a", "a"), null) }), default);

        result.Error!.Code.Should().Be("E-AUM-NOT-FOUND");
    }

    [Fact]
    public async Task UpdateReferenceFile_RemovingReferencedEntry_WithoutConfirmation_Throws()
    {
        var file = SeedFile("a");
        var usage = Substitute.For<IReferenceUsageChecker>();
        usage.CheckAsync("skills", Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(new ReferenceUsageResult(true, new[] { "a" }));
        var handler = new UpdateReferenceFileHandler(_store, usage, Kit.User(), _clock);

        var act = () => handler.Handle(new UpdateReferenceFileCommand("Skills", false, new[] { new ReferenceChangeRequest("remove", file.Entries.Single().Id, null, null, null) }), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-AUM-ENTRY-IN-USE");
    }

    [Fact]
    public async Task UpdateReferenceFile_WhenUsageUnknown_FailsSafeAndRequiresConfirmation()
    {
        var file = SeedFile("a");
        var usage = Substitute.For<IReferenceUsageChecker>();
        usage.CheckAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(new ReferenceUsageResult(false, Array.Empty<string>()));
        var handler = new UpdateReferenceFileHandler(_store, usage, Kit.User(), _clock);

        var act = () => handler.Handle(new UpdateReferenceFileCommand("skills", false, new[] { new ReferenceChangeRequest("remove", file.Entries.Single().Id, null, null, null) }), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.Code.Should().Be("PA.Reference.ENTRY_IN_USE");
    }

    [Fact]
    public async Task UpdateReferenceFile_WithConfirmation_RemovesWithoutAskingOwners()
    {
        var file = SeedFile("a");
        var usage = Substitute.For<IReferenceUsageChecker>();
        var handler = new UpdateReferenceFileHandler(_store, usage, Kit.User(), _clock);

        var result = await handler.Handle(new UpdateReferenceFileCommand("skills", true, new[] { new ReferenceChangeRequest("remove", file.Entries.Single().Id, null, null, null) }), default);

        result.Value.Version.Should().Be(3);
        file.Entries.Single().IsActive.Should().BeFalse();
        await usage.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdateReferenceFile_AddingEntries_DoesNotCallOwners()
    {
        SeedFile();
        var usage = Substitute.For<IReferenceUsageChecker>();
        var handler = new UpdateReferenceFileHandler(_store, usage, Kit.User(), _clock);

        var result = await handler.Handle(new UpdateReferenceFileCommand("skills", false, new[] { new ReferenceChangeRequest("add", null, "b", new("ب", "b"), null) }), default);

        result.IsSuccess.Should().BeTrue();
        await usage.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdateTaxonomy_UnknownType_CreatesItAndStoresSnapshotsPerVersion()
    {
        var handler = new UpdatePlatformTaxonomyHandler(_store, Kit.User(), _clock);

        var result = await handler.Handle(new UpdatePlatformTaxonomyCommand("Certifications", new[] { new TaxonomyChangeRequest("add", "pmp", new("ا", "PMP"), null, null, null) }), default);

        result.Value.Version.Should().Be(2);
        _store.Taxonomies.Single().Type.Should().Be("certifications");
        _store.Snapshots.Select(s => s.TaxonomyVersion).Should().Equal(1, 2);
    }

    [Fact]
    public async Task UpdateTaxonomy_ExistingType_BumpsVersionAndAddsSnapshot()
    {
        var taxonomy = PlatformTaxonomy.Create("skills", _clock.GetUtcNow().UtcDateTime);
        _store.Taxonomies.Add(taxonomy);
        var handler = new UpdatePlatformTaxonomyHandler(_store, Kit.User(), _clock);

        var result = await handler.Handle(new UpdatePlatformTaxonomyCommand("skills", new[] { new TaxonomyChangeRequest("add", "sql", new("ا", "SQL"), null, new[] { " structured query " }, null) }), default);

        result.Value.Version.Should().Be(2);
        taxonomy.Nodes.Single().Synonyms.Should().Equal("structured query");
        _store.Snapshots.Should().ContainSingle().Which.TaxonomyVersion.Should().Be(2);
    }

    [Fact]
    public async Task SuspendOffering_UnknownId_ReturnsNotFound()
    {
        var handler = new SuspendJobOfferingHandler(_store, Kit.User(), _clock);

        var result = await handler.Handle(new SuspendJobOfferingCommand(Guid.NewGuid(), "reason!"), default);

        result.Error!.Code.Should().Be("E-AUM-NOT-FOUND");
    }

    [Fact]
    public async Task SuspendOffering_ThenAgain_SecondThrowsStateInactive()
    {
        var offering = JobOffering.Register(Guid.NewGuid(), Guid.NewGuid(), "Engineer", _clock.GetUtcNow().UtcDateTime);
        _store.Offerings.Add(offering);
        var handler = new SuspendJobOfferingHandler(_store, Kit.User(), _clock);

        (await handler.Handle(new SuspendJobOfferingCommand(offering.Id, "Policy breach"), default)).IsSuccess.Should().BeTrue();
        var act = () => handler.Handle(new SuspendJobOfferingCommand(offering.Id, "Policy breach"), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-AUM-STATE-INACTIVE");
    }

    [Fact]
    public async Task RemoveOffering_RecordsRemoval()
    {
        var offering = JobOffering.Register(Guid.NewGuid(), Guid.NewGuid(), "Engineer", _clock.GetUtcNow().UtcDateTime);
        _store.Offerings.Add(offering);

        (await new RemoveJobOfferingHandler(_store, Kit.User(), _clock).Handle(new RemoveJobOfferingCommand(offering.Id, "Fraudulent"), default)).IsSuccess.Should().BeTrue();

        offering.Moderation.Should().Be(ModerationKind.Removed);
        (await new RemoveJobOfferingHandler(_store, Kit.User(), _clock).Handle(new RemoveJobOfferingCommand(Guid.NewGuid(), "Fraudulent"), default)).Error!.Type.Should().Be(ErrorType.NotFound);
    }

    private static JobPostingCreatedIntegrationEvent Created(Guid id) =>
        new(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, id, Guid.NewGuid(), Guid.NewGuid(), "draft", "Engineer", "it", new[] { "sql" }, "public", "Employer", null, null, null, 1);

    [Fact]
    public async Task RegisterJobOffering_CreatesActiveOffering_AndIsIdempotentPerPosting()
    {
        var handler = new RegisterJobOfferingHandler(_store, _clock);
        var id = Guid.NewGuid();

        await handler.Handle(Created(id), default);
        await handler.Handle(Created(id), default);

        var offering = _store.Offerings.Should().ContainSingle().Which;
        (offering.Id, offering.Title, offering.Status).Should().Be((id, "Engineer", JobOfferingStatus.Active));
    }

    [Fact]
    public async Task RegisterJobOffering_ThenSuspend_SuspensionAppliesToRegisteredOffering()
    {
        var id = Guid.NewGuid();
        await new RegisterJobOfferingHandler(_store, _clock).Handle(Created(id), default);

        var result = await new SuspendJobOfferingHandler(_store, Kit.User(), _clock).Handle(new SuspendJobOfferingCommand(id, "Inappropriate"), default);

        result.IsSuccess.Should().BeTrue();
    }
}
