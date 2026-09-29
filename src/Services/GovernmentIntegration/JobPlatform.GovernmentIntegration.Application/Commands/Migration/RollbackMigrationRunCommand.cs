using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Migration;

public sealed record RollbackMigrationRunCommand(Guid MigrationRunId, string Reason) : AdminCommand<Unit>;
