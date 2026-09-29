using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Migration;

public sealed record CleanseMigratedDataCommand(
    Guid MigrationRunId, Guid BatchId, string SnapshotVersion, int IssuesResolved, int DuplicatesRemoved, int FormatsStandardized,
    int RecordsChecked, int RecordsRejected) : ServiceCommand<DataQualityView>;
