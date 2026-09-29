namespace JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

public sealed record DataQualityView(
    Guid Id, Guid MigrationRunId, Guid BatchId, string Status, int IssuesResolved, int DuplicatesRemoved, int FormatsStandardized,
    int RecordsChecked, int RecordsRejected);
