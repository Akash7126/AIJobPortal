namespace JobPlatform.GovernmentIntegration.Application.DTOs.Migration;

public sealed record MigrationRunView(Guid Id, Guid InitiatedBy, string Status, IReadOnlyList<MigrationPhaseView> Phases,
    IReadOnlyList<MigrationLogEntryView> Log);
