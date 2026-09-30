namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IMigrationRunRepository
{
    Task<MigrationRun?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Single-run guard (handover section 3.7): a run that is Created/Running/PhaseFailed, if any.</summary>
    Task<MigrationRun?> GetActiveAsync(CancellationToken ct = default);

    void Add(MigrationRun run);
}
