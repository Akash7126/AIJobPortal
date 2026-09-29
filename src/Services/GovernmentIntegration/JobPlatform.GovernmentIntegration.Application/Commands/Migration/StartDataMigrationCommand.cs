using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Commands.Migration;

public sealed record StartDataMigrationCommand(IReadOnlyList<string> Phases, bool DryRun) : AdminCommand<MigrationRunView>;
