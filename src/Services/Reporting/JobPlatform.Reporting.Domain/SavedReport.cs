using System.Text.Json;
using System.Text.Json.Serialization;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>
/// A report definition saved to the owner's library (US-3.5.4-06/07). INV-05: only the owner may read, run or delete it (E-CRG-FORBIDDEN); default visibility
/// is the owner's own reports (A-02-015). Kept 12 months, then archived (AC-03). Re-runnable.
/// </summary>
public sealed class SavedReport : AggregateRoot<Guid>
{
    public const int RetentionMonths = 12;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private SavedReport()
    {
    }

    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string DefinitionJson { get; private set; } = "{}";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime RetainUntilUtc { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    public ReportDefinition Definition => JsonSerializer.Deserialize<ReportDefinition>(DefinitionJson, Json)!;

    public static SavedReport Save(Guid ownerId, string name, ReportDefinition definition, DateTime nowUtc)
    {
        Guard.Ensure(ownerId != Guid.Empty, ReportingRuleCodes.SavedReportNotOwner, "A saved report needs an owner.", ReportingErrorCodes.CustomForbidden, BusinessRuleKind.Forbidden);
        Guard.Ensure(!string.IsNullOrWhiteSpace(name) && name.Length <= 150, ReportingRuleCodes.InvalidDefinition, "A saved report needs a name of up to 150 characters.",
            ReportingErrorCodes.CustomInvalidField, BusinessRuleKind.InvalidInput);
        var normalised = definition.ValidateAndNormalise();
        return new SavedReport
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name.Trim(),
            DefinitionJson = JsonSerializer.Serialize(normalised, Json),
            CreatedAtUtc = nowUtc,
            RetainUntilUtc = nowUtc.AddMonths(RetentionMonths)
        };
    }

    /// <summary>INV-05 NOT_OWNER: 403 E-CRG-FORBIDDEN. The report's existence is not disclosed beyond that.</summary>
    public void EnsureOwnedBy(Guid userId) =>
        Guard.Ensure(OwnerId == userId, ReportingRuleCodes.SavedReportNotOwner, "Only the owner may use this saved report.", ReportingErrorCodes.CustomForbidden,
            BusinessRuleKind.Forbidden);

    public void Archive(DateTime nowUtc)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        ArchivedAtUtc = nowUtc;
    }
}
