using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum LegacyStage
{
    Imported,
    Mapped,
    Transformed,
    Validated,
    Migrated,
    Invalid
}

/// <summary>AGG-35: a record read from a predecessor MoL/PEF system, being mapped to the new schema (handover section 3.5, story US-6.1-02).
/// <see cref="MigrationBatchId"/> tags the staging rollback unit (handover section 3.7, proposed rollback mechanism).</summary>
public sealed class LegacyData : AggregateRoot<Guid>
{
    private LegacyData()
    {
    }

    public Guid MigrationRunId { get; private set; }
    public Guid MigrationBatchId { get; private set; }
    public SourceSystem SourceSystem { get; private set; }
    public string SourceRecordId { get; private set; } = string.Empty;
    public string RecordType { get; private set; } = string.Empty;
    public string SourcePayload { get; private set; } = string.Empty;
    public string? MappedPayload { get; private set; }
    public LegacyStage Stage { get; private set; }

    /// <summary>Settable so the set can be mapped as one JSON column (handover section 8.1) - a value object with no identity.</summary>
    public IReadOnlyCollection<string> ValidationErrors { get; private set; } = Array.Empty<string>();

    /// <summary>INV-11 (idempotency on SourceSystem+SourceRecordId) is enforced by the repository/unique index: the application looks up
    /// <c>GetBySourceKeyAsync</c> first and leaves an existing record unchanged on a re-import (AC-04) rather than calling this factory again.</summary>
    public static LegacyData Import(Guid id, Guid migrationRunId, Guid migrationBatchId, SourceSystem sourceSystem, string sourceRecordId,
        string recordType, string sourcePayload) =>
        new()
        {
            Id = id,
            MigrationRunId = migrationRunId,
            MigrationBatchId = migrationBatchId,
            SourceSystem = sourceSystem,
            SourceRecordId = sourceRecordId,
            RecordType = recordType,
            SourcePayload = sourcePayload,
            Stage = LegacyStage.Imported
        };

    public void MapToNewSchema(string mappedPayload)
    {
        Check(StageRule(LegacyStage.Imported, "Mapping requires the record to have just been imported."));
        MappedPayload = mappedPayload;
        Stage = LegacyStage.Mapped;
    }

    public void Transform(string transformedPayload)
    {
        Check(StageRule(LegacyStage.Mapped, "Transformation requires the record to have been mapped first."));
        MappedPayload = transformedPayload;
        Stage = LegacyStage.Transformed;
    }

    /// <summary>Fails ⇒ Invalid, code E-DMIG-INVALID-FIELD, excluded from the completed migration set (AC-03).</summary>
    public void Validate(IReadOnlyCollection<string> errors)
    {
        Check(StageRule(LegacyStage.Transformed, "Validation requires the record to have been transformed first."));
        if (errors.Count > 0)
        {
            ValidationErrors = errors.ToList();
            Stage = LegacyStage.Invalid;
            return;
        }

        Stage = LegacyStage.Validated;
    }

    /// <summary>INV-12: only after mapping, transformation and validation have all completed.</summary>
    public void MarkMigrated(DateTime nowUtc)
    {
        Check(new BusinessRule(RuleCodes.LegacyNotFullyMapped, "The record must be mapped, transformed and validated before it can be migrated.",
            Stage != LegacyStage.Validated, ErrorCodes.NotFullyMapped, BusinessRuleKind.BusinessRule));
        Stage = LegacyStage.Migrated;
        Raise(new LegacyDataImportedDomainEvent(Id, MigrationRunId, RecordType, SourceSystem.ToString(), nowUtc));
    }

    private IBusinessRule StageRule(LegacyStage expected, string message) =>
        new BusinessRule(RuleCodes.LegacyNotFullyMapped, message, Stage != expected, ErrorCodes.NotFullyMapped, BusinessRuleKind.BusinessRule);
}
