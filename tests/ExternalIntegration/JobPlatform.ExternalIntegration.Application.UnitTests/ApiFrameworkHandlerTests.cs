using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.ExternalIntegration.Application.UnitTests;

public class ApiFrameworkHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.4.3-01")]
    public async Task ReleaseApiVersionHandler_NewVersion_Succeeds()
    {
        var store = new FakeStore();
        var handler = new ReleaseApiVersionHandler(store);

        var result = await handler.Handle(new ReleaseApiVersionCommand("v1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.ApiVersions.Should().ContainSingle(v => v.Id == "v1");
    }

    [Fact]
    [Trait("Story", "US-3.4.3-05")]
    public async Task DeprecateApiVersionHandler_ExistingActiveVersion_Succeeds()
    {
        var store = new FakeStore();
        store.ApiVersions.Add(ApiVersion.Release("v1"));
        var handler = new DeprecateApiVersionHandler(store, Kit.User(ActorType.Administrator), Kit.Clock());

        var result = await handler.Handle(new DeprecateApiVersionCommand("v1", Kit.Clock().GetUtcNow().UtcDateTime.AddDays(90)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.ApiVersions.Single().Status.Should().Be(ApiVersionStatus.Deprecated);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-05")]
    [Trait("AC", "AC-04")]
    public async Task RetireApiVersionHandler_BeforeSunset_Throws()
    {
        var store = new FakeStore();
        var version = ApiVersion.Release("v1");
        version.Deprecate(Kit.Clock().GetUtcNow().UtcDateTime.AddDays(90), Kit.Clock().GetUtcNow().UtcDateTime, new Actor(Guid.NewGuid(), true));
        store.ApiVersions.Add(version);
        var handler = new RetireApiVersionHandler(store, Kit.User(ActorType.Administrator), Kit.Clock());

        var act = () => handler.Handle(new RetireApiVersionCommand("v1"), CancellationToken.None);

        (await act.Should().ThrowAsync<SharedKernel.Domain.BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.ApiRetireBeforeSunset);
    }

    [Fact]
    [Trait("Story", "US-4.3-01")]
    [Trait("AC", "AC-05")]
    public async Task RegisterSoftwareInterfaceHandler_CalledTwice_IsIdempotent()
    {
        var store = new FakeStore();
        var handler = new RegisterSoftwareInterfaceHandler(store, Kit.User(ActorType.Administrator));

        await handler.Handle(new RegisterSoftwareInterfaceCommand("ExternalJobSite", "Jobs4All", "https://jobs4all.example/api"), CancellationToken.None);
        var second = await handler.Handle(new RegisterSoftwareInterfaceCommand("ExternalJobSite", "Jobs4All", "https://jobs4all.example/api/v2"),
            CancellationToken.None);

        store.SoftwareInterfaces.Should().ContainSingle();
        second.Value.Endpoint.Should().Be("https://jobs4all.example/api/v2");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-12")]
    public async Task ViewApiSchemaDocumentationHandler_WithoutActiveCredential_ReturnsForbidden()
    {
        var store = new FakeStore();
        store.ApiVersions.Add(ApiVersion.Release("v1"));
        var handler = new ViewApiSchemaDocumentationHandler(store, store, store, Kit.User(), Kit.Clock());

        var result = await handler.Handle(new ViewApiSchemaDocumentationCommand("v1"), CancellationToken.None);

        result.Error!.Code.Should().Be(ErrorCodes.PartnerForbidden);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-12")]
    [Trait("AC", "AC-04")]
    public async Task ViewApiSchemaDocumentationHandler_WithActiveCredential_PublishesViewEvent()
    {
        var partnerId = Guid.NewGuid();
        var store = new FakeStore();
        store.ApiVersions.Add(ApiVersion.Release("v1"));
        store.Credentials.Add(new PartnerCredential(Guid.NewGuid(), partnerId, Kit.Clock().GetUtcNow().UtcDateTime.AddDays(1), 1));
        var handler = new ViewApiSchemaDocumentationHandler(store, store, store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new ViewApiSchemaDocumentationCommand("v1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.AccessLogs.Should().ContainSingle().Which.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ApiSchemaDocumentationViewedDomainEvent>();
    }
}
