using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Migration;

public sealed record ImportLegacyDataBatchCommand(Guid MigrationRunId, Guid MigrationBatchId, SourceSystem SourceSystem, int Take)
    : ServiceCommand<int>;
