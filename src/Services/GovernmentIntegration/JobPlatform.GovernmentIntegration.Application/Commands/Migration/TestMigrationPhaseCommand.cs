using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Migration;

public sealed record TestMigrationPhaseCommand(Guid MigrationRunId, bool Passed, string TestOutcome) : ServiceCommand<MigrationRunView>;
