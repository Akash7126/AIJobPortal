using JobPlatform.PlatformAdministration.Application;
using JobPlatform.PlatformAdministration.Application.Events;
using JobPlatform.PlatformAdministration.Application.Reference;
using JobPlatform.PlatformAdministration.Application.Settings;
using JobPlatform.PlatformAdministration.Application.Taxonomy;
using JobPlatform.PlatformAdministration.Application.Users;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;
using NSubstitute;

namespace JobPlatform.PlatformAdministration.Application.UnitTests;

public class QueryAndEventTests
{
    private readonly FakeCache _cache = new();
    private readonly IAdminReadStore _store = Substitute.For<IAdminReadStore>();

    private static TaxonomyView View(int version) => new(Guid.NewGuid(), "skills", version, new[] { new TaxonomyNodeView("sql", new("ا", "SQL"), null, Array.Empty<string>(), true) });

    [Fact]
    public async Task ConsumerTaxonomy_CurrentVersion_ReadsPointerThenVersionAndCachesBoth()
    {
        _store.GetTaxonomyCurrentVersionAsync("skills", Arg.Any<CancellationToken>()).Returns(3);
        _store.GetTaxonomyAsync("skills", 3, Arg.Any<CancellationToken>()).Returns(View(3));
        var handler = new GetTaxonomyForConsumersHandler(new TaxonomyReader(_store, _cache));

        var first = await handler.Handle(new GetTaxonomyForConsumersQuery("Skills"), default);
        var second = await handler.Handle(new GetTaxonomyForConsumersQuery("skills"), default);

        first.Value.Version.Should().Be(3);
        second.Value.Version.Should().Be(3);
        _cache.Items.Keys.Should().BeEquivalentTo("taxonomy:skills:current", "taxonomy:skills:v3");
        await _store.Received(1).GetTaxonomyCurrentVersionAsync("skills", Arg.Any<CancellationToken>());
        await _store.Received(1).GetTaxonomyAsync("skills", 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumerTaxonomy_SpecificVersion_SkipsThePointer()
    {
        _store.GetTaxonomyAsync("skills", 2, Arg.Any<CancellationToken>()).Returns(View(2));
        var handler = new GetTaxonomyForConsumersHandler(new TaxonomyReader(_store, _cache));

        var result = await handler.Handle(new GetTaxonomyForConsumersQuery("skills", 2), default);

        result.Value.Version.Should().Be(2);
        await _store.DidNotReceive().GetTaxonomyCurrentVersionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("nope", null)]
    [InlineData("skills", 9)]
    [InlineData("BAD TYPE", null)]
    public async Task ConsumerTaxonomy_UnknownTypeOrVersion_ReturnsNotFound(string type, int? version)
    {
        var handler = new GetTaxonomyForConsumersHandler(new TaxonomyReader(_store, _cache));

        var result = await handler.Handle(new GetTaxonomyForConsumersQuery(type, version), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task AdminTaxonomy_ReadsDirectlyFromTheStoreWithoutCaching()
    {
        _store.GetTaxonomyCurrentVersionAsync("skills", Arg.Any<CancellationToken>()).Returns(2);
        _store.GetTaxonomyAsync("skills", 2, Arg.Any<CancellationToken>()).Returns(View(2));

        var result = await new GetPlatformTaxonomyHandler(new TaxonomyReader(_store, _cache)).Handle(new GetPlatformTaxonomyQuery("skills"), default);

        result.IsSuccess.Should().BeTrue();
        _cache.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ReferenceFile_ConsumerReadIsCached_AndUnknownTypeIs404()
    {
        var view = new ReferenceFileView(Guid.NewGuid(), "skills", 1, Array.Empty<ReferenceEntryView>());
        _store.GetReferenceFileAsync("skills", Arg.Any<CancellationToken>()).Returns(view);
        var handler = new GetReferenceFileForConsumersHandler(new ReferenceFileReader(_store, _cache));

        (await handler.Handle(new GetReferenceFileForConsumersQuery("Skills"), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new GetReferenceFileForConsumersQuery("skills"), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new GetReferenceFileForConsumersQuery("bogus"), default)).Error!.Type.Should().Be(ErrorType.NotFound);

        await _store.Received(1).GetReferenceFileAsync("skills", Arg.Any<CancellationToken>());
        _cache.Items.Should().ContainKey("reference:skills");
    }

    [Fact]
    public async Task Setting_ConsumerReadIsCached_AndUnknownKeyIs404()
    {
        var view = new SettingView("upload.maxSizeMb", "Int", "5", 1, new(1, 50, Array.Empty<string>(), null), null, DateTime.UtcNow);
        _store.GetSettingAsync("upload.maxSizeMb", Arg.Any<CancellationToken>()).Returns(view);
        var handler = new GetSystemSettingHandler(_store, _cache);

        (await handler.Handle(new GetSystemSettingQuery("upload.maxSizeMb"), default)).Value.Value.Should().Be("5");
        (await handler.Handle(new GetSystemSettingQuery("upload.maxSizeMb"), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new GetSystemSettingQuery("ghost"), default)).Error!.Type.Should().Be(ErrorType.NotFound);

        await _store.Received(1).GetSettingAsync("upload.maxSizeMb", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListUsers_DelegatesToTheDirectoryWithFilters()
    {
        var directory = Substitute.For<IUserDirectory>();
        var page = new PagedResult<PlatformUserListItem>(Array.Empty<PlatformUserListItem>(), 2, 10, 0);
        directory.ListAsync("Employer", "Active", "acme", Arg.Is<PageRequest>(p => p.Page == 2 && p.PageSize == 10), Arg.Any<CancellationToken>()).Returns(page);

        var result = await new ListPlatformUsersHandler(directory).Handle(new ListPlatformUsersQuery("acme", "Employer", "Active", 2, 10), default);

        result.Value.Should().Be(page);
    }

    // ------------------------------------------------------------------ event mapping and cache eviction

    private static readonly DomainEventContext Ctx = new("agg-1", 7, Guid.NewGuid(), Guid.NewGuid());
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Mapper_MapsAllFivePublishedEvents()
    {
        var mapper = new PlatformAdministrationEventMapper();
        var actor = Guid.NewGuid();
        var id = Guid.NewGuid();

        var record = mapper.Map(new PlatformEntityRecordCreatedDomainEvent(id, PlatformEntityType.Employer, actor, At), Ctx).Should().BeOfType<PlatformEntityRecordCreatedIntegrationEvent>().Which;
        (record.PlatformEntityRecordId, record.EntityType, record.AggregateVersion, record.RoutingKey).Should().Be((id, "Employer", 7L, "platform-entity-record.created.v1"));

        var taxonomy = mapper.Map(new PlatformTaxonomyUpdatedDomainEvent(id, "skills", 2, 3, new[] { "sql" }, actor, At), Ctx).Should().BeOfType<PlatformTaxonomyUpdatedIntegrationEvent>().Which;
        (taxonomy.TaxonomyType, taxonomy.FromVersion, taxonomy.ToVersion, taxonomy.FromStatus, taxonomy.ToStatus).Should().Be(("skills", 2, 3, "v2", "v3"));
        taxonomy.ChangedCodes.Should().Equal("sql");
        taxonomy.CorrelationId.Should().Be(Ctx.CorrelationId);

        var offering = mapper.Map(new JobOfferingSuspendedDomainEvent(id, actor, "reason", ModerationKind.Suspended, At), Ctx).Should().BeOfType<JobOfferingSuspendedIntegrationEvent>().Which;
        (offering.JobOfferingId, offering.JobPostingId, offering.Reason).Should().Be((id, id, "reason"));

        var file = mapper.Map(new ReferenceFileUpdatedDomainEvent(id, ReferenceFileType.Skills, 4, actor, At), Ctx).Should().BeOfType<ReferenceFileUpdatedIntegrationEvent>().Which;
        (file.Type, file.FileVersion).Should().Be(("skills", 4));

        var setting = mapper.Map(new SystemSettingChangedDomainEvent(id, "upload.maxSizeMb", 3, actor, At), Ctx).Should().BeOfType<SystemSettingChangedIntegrationEvent>().Which;
        (setting.Key, setting.SettingVersion).Should().Be(("upload.maxSizeMb", 3));
    }

    [Fact]
    public void Mapper_IgnoresUnknownEvents() => new PlatformAdministrationEventMapper().Map(Substitute.For<JobPlatform.SharedKernel.Domain.IDomainEvent>(), Ctx).Should().BeNull();

    [Fact]
    public async Task DomainEventHandlers_EvictTheMatchingCacheKeys()
    {
        _cache.Items["taxonomy:skills:current"] = 1;
        _cache.Items["taxonomy:skills:v1"] = 1;
        _cache.Items["reference:skills"] = 1;
        _cache.Items["setting:upload.maxSizeMb"] = 1;

        await new TaxonomyUpdatedCacheHandler(_cache).Handle(new PlatformTaxonomyUpdatedDomainEvent(Guid.NewGuid(), "skills", 1, 2, Array.Empty<string>(), Guid.NewGuid(), At), default);
        await new ReferenceFileUpdatedCacheHandler(_cache).Handle(new ReferenceFileUpdatedDomainEvent(Guid.NewGuid(), ReferenceFileType.Skills, 2, Guid.NewGuid(), At), default);
        await new SystemSettingChangedCacheHandler(_cache).Handle(new SystemSettingChangedDomainEvent(Guid.NewGuid(), "upload.maxSizeMb", 2, Guid.NewGuid(), At), default);

        _cache.Items.Keys.Should().Equal("taxonomy:skills:v1");
    }
}
