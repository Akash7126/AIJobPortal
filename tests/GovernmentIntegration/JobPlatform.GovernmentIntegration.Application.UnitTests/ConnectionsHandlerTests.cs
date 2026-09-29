using JobPlatform.GovernmentIntegration.Application.Commands.Connections;
using JobPlatform.GovernmentIntegration.Application.Handlers.Connections;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Application.UnitTests;

public class ConnectionsHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.4.2-02")]
    [Trait("AC", "AC-04")]
    public async Task ConfigureGovernmentSourceConnection_FirstCall_Creates()
    {
        var store = new FakeStore();
        var handler = new ConfigureGovernmentSourceConnectionHandler(store, Kit.User(ActorType.Administrator));

        var result = await handler.Handle(
            new ConfigureGovernmentSourceConnectionCommand(SourceSystem.MoL, "https://mol.example/api", "ApiKey", "ref-1", true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Connections.Should().ContainSingle(c => c.Source == SourceSystem.MoL);
    }

    [Fact]
    [Trait("Story", "US-2.5-04")]
    [Trait("AC", "AC-04")]
    public async Task ConfigureGovernmentSourceConnection_ReappliedTwice_IsIdempotentUpsert()
    {
        var store = new FakeStore();
        var handler = new ConfigureGovernmentSourceConnectionHandler(store, Kit.User(ActorType.Administrator));
        var command = new ConfigureGovernmentSourceConnectionCommand(SourceSystem.PEF, "https://pef.example/api", "ApiKey", "ref-1", true);

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        store.Connections.Should().ContainSingle(c => c.Source == SourceSystem.PEF);
    }

    [Fact]
    [Trait("Story", "US-2.5-01")]
    public async Task RunSourceReconciliation_Success_RecordsSyncSuccess()
    {
        var store = new FakeStore();
        var connection = GovernmentSourceConnection.Configure(Guid.NewGuid(), new Domain.Common.Actor(Guid.NewGuid(), true), SourceSystem.MoL,
            "https://mol.example/api", "ApiKey", "ref", true);
        store.Add(connection);
        var handler = new RunSourceReconciliationHandler(store, new FakeMolRegistryClient(), new FakePefClient(), Kit.Clock());

        var result = await handler.Handle(new RunSourceReconciliationCommand(SourceSystem.MoL), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        connection.Health.Should().Be(ConnectionHealth.Healthy);
        connection.LastSuccessfulSyncAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RunSourceReconciliation_UnconfiguredSource_ReturnsNotFound()
    {
        var store = new FakeStore();
        var handler = new RunSourceReconciliationHandler(store, new FakeMolRegistryClient(), new FakePefClient(), Kit.Clock());

        var result = await handler.Handle(new RunSourceReconciliationCommand(SourceSystem.MoL), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }
}
