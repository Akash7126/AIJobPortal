using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.UnitTests;

public class MappingHandlerTests
{
    private static (FakeStore Store, ExternalJobSiteIntegration Integration) Registered(Guid partnerId)
    {
        var store = new FakeStore();
        var integration = ExternalJobSiteIntegration.Register(Guid.NewGuid(), partnerId, new SourcePlatform(Guid.NewGuid(), "X", "https://x.example"), true,
            Kit.Clock().GetUtcNow().UtcDateTime);
        store.Integrations.Add(integration);
        return (store, integration);
    }

    private static MappingRuleInput[] ValidRules() => new[]
    {
        new MappingRuleInput("t", "title", "None"), new MappingRuleInput("s", "summary", "None"), new MappingRuleInput("sk", "skills", "SplitComma")
    };

    [Fact]
    [Trait("Story", "US-3.1.3-04")]
    public async Task ConfigureJobDataMappingHandler_FirstConfiguration_CreatesMapping()
    {
        var partnerId = Guid.NewGuid();
        var (store, integration) = Registered(partnerId);
        var handler = new ConfigureJobDataMappingHandler(store, store, Kit.User(id: partnerId), Kit.Clock());

        var result = await handler.Handle(new ConfigureJobDataMappingCommand(ValidRules(), "v1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(1);
        integration.MappingId.Should().NotBeNull();
    }

    [Fact]
    [Trait("Story", "US-3.4.1-03")]
    [Trait("AC", "AC-01")]
    public async Task ConfigureJobDataMappingHandler_SecondConfiguration_IncrementsVersion()
    {
        var partnerId = Guid.NewGuid();
        var (store, _) = Registered(partnerId);
        var handler = new ConfigureJobDataMappingHandler(store, store, Kit.User(id: partnerId), Kit.Clock());
        await handler.Handle(new ConfigureJobDataMappingCommand(ValidRules(), "v1"), CancellationToken.None);

        var result = await handler.Handle(new ConfigureJobDataMappingCommand(ValidRules(), "v1"), CancellationToken.None);

        result.Value.Version.Should().Be(2);
        store.Mappings.Should().ContainSingle();
    }

    [Fact]
    public async Task GetJobDataMappingHandler_WhenNoneConfigured_ReturnsNotFound()
    {
        var partnerId = Guid.NewGuid();
        var (store, _) = Registered(partnerId);
        var handler = new GetJobDataMappingHandler(store, store, Kit.User(id: partnerId));

        var result = await handler.Handle(new GetJobDataMappingQuery(), CancellationToken.None);

        result.Error!.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task GetStandardSchemaHandler_ReturnsRequiredFields()
    {
        var handler = new GetStandardSchemaHandler();

        var result = await handler.Handle(new GetStandardSchemaQuery(), CancellationToken.None);

        result.Value.Fields.Where(f => f.Required).Select(f => f.Field).Should().BeEquivalentTo(new[] { "title", "summary", "skills" });
    }
}
