using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Queries.Migration;

public sealed record GetMigrationRunQuery(Guid MigrationRunId) : AdminQuery<MigrationRunView>;
