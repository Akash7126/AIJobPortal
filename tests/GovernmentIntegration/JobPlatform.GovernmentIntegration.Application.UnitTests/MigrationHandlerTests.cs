using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Application.UnitTests;

public class MigrationHandlerTests
{
    [Fact]
    [Trait("Story", "US-6.1-01")]
    [Trait("AC", "AC-04")]
    public async Task StartDataMigration_ByAdministrator_CreatesRun()
    {
        var store = new FakeStore();
        var handler = new StartDataMigrationHandler(store, Kit.User(ActorType.Administrator), Kit.Clock());

        var result = await handler.Handle(new StartDataMigrationCommand(new[] { "Import", "Cleanse" }, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.MigrationRuns.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-6.1-01")]
    public async Task StartDataMigration_WhileAnotherIsActive_ReturnsConflict()
    {
        var store = new FakeStore();
        var admin = Kit.User(ActorType.Administrator);
        var handler = new StartDataMigrationHandler(store, admin, Kit.Clock());
        await handler.Handle(new StartDataMigrationCommand(new[] { "Import" }, false), CancellationToken.None);

        var result = await handler.Handle(new StartDataMigrationCommand(new[] { "Import" }, false), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.MigrationAlreadyActive);
    }

    [Fact]
    [Trait("Story", "US-6.1-03")]
    [Trait("AC", "AC-02")]
    public async Task RollbackMigrationRun_AfterPhaseFailure_Succeeds()
    {
        var store = new FakeStore();
        var run = MigrationRun.Start(Guid.NewGuid(), new Domain.Common.Actor(Guid.NewGuid(), true), new[] { "Import" }, Kit.Clock().GetUtcNow().UtcDateTime);
        run.FailPhase("boom", Kit.Clock().GetUtcNow().UtcDateTime);
        store.Add(run);
        var handler = new RollbackMigrationRunHandler(store, store, Kit.User(ActorType.Administrator), Kit.Clock());

        var result = await handler.Handle(new RollbackMigrationRunCommand(run.Id, "bad data"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(MigrationStatus.RolledBack);
    }

    [Fact]
    [Trait("Story", "US-6.1-02")]
    [Trait("AC", "AC-04")]
    public async Task ImportLegacyDataBatch_DuplicateSourceRecord_IsLeftUnchanged()
    {
        var store = new FakeStore();
        var migrationRunId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var reader = new FakeLegacySourceReader
        {
            NextBatch = new[] { new LegacySourceRecord("SRC-1", "JobSeeker", "{\"name\":\"Sara\"}") }
        };
        var handler = new ImportLegacyDataBatchHandler(store, reader, Kit.Clock());
        var command = new ImportLegacyDataBatchCommand(migrationRunId, batchId, SourceSystem.MoL, 10);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.Value.Should().Be(1);
        second.Value.Should().Be(0);
        store.LegacyData.Should().ContainSingle();
        store.LegacyData.Single().Stage.Should().Be(LegacyStage.Migrated);
    }

    [Fact]
    [Trait("Story", "US-6.1-02")]
    [Trait("AC", "AC-03")]
    public async Task ImportLegacyDataBatch_EmptyPayload_IsExcludedAsInvalid()
    {
        var store = new FakeStore();
        var reader = new FakeLegacySourceReader { NextBatch = new[] { new LegacySourceRecord("SRC-2", "JobSeeker", "") } };
        var handler = new ImportLegacyDataBatchHandler(store, reader, Kit.Clock());

        var result = await handler.Handle(new ImportLegacyDataBatchCommand(Guid.NewGuid(), Guid.NewGuid(), SourceSystem.PEF, 10), CancellationToken.None);

        result.Value.Should().Be(0);
        store.LegacyData.Single().Stage.Should().Be(LegacyStage.Invalid);
    }

    [Fact]
    [Trait("Story", "US-6.1-04")]
    [Trait("AC", "AC-03")]
    public async Task CleanseMigratedData_WhileAlreadyRunningForBatch_ReturnsConflict()
    {
        var store = new FakeStore();
        var batchId = Guid.NewGuid();
        store.Add(DataQuality.Start(Guid.NewGuid(), Guid.NewGuid(), batchId, "v1"));
        var handler = new CleanseMigratedDataHandler(store, Kit.Clock());

        var result = await handler.Handle(new CleanseMigratedDataCommand(Guid.NewGuid(), batchId, "v2", 1, 1, 1, 10, 0), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.DataQualitySnapshotIsolation);
    }

    [Fact]
    [Trait("Story", "US-6.1-04")]
    [Trait("AC", "AC-02")]
    public async Task CleanseMigratedData_NoConflict_CompletesAndPublishesUpdate()
    {
        var store = new FakeStore();
        var handler = new CleanseMigratedDataHandler(store, Kit.Clock());

        var result = await handler.Handle(new CleanseMigratedDataCommand(Guid.NewGuid(), Guid.NewGuid(), "v1", 2, 1, 1, 20, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Completed");
        store.DataQuality.Single().RecordsChecked.Should().Be(20);
    }
}
