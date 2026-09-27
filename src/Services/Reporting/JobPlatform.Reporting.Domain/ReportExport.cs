using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>
/// Report export (US-3.5.4-05): Queued, Generating, then Ready or Failed. INV-06: an identical request from the same administrator (same reference, parameters and
/// format) while one is Queued or Generating reuses that job (AC-03). Enforced by the handler through the repository and by a filtered unique index as the final guard.
/// </summary>
public sealed class ReportExport : AggregateRoot<Guid>
{
    private ReportExport()
    {
    }

    public Guid RequestedBy { get; private set; }
    public ReportRefKind RefKind { get; private set; }
    public Guid? RefId { get; private set; }
    public string ParametersJson { get; private set; } = "{}";
    public string ParametersHash { get; private set; } = string.Empty;
    public ReportFormat Format { get; private set; }
    public ExportStatus Status { get; private set; }
    public string? ResultRef { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public bool IsInProgress => Status is ExportStatus.Queued or ExportStatus.Generating;

    public IReadOnlyDictionary<string, string> Parameters =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson) ?? new Dictionary<string, string>();

    /// <summary>Stable hash: two identical requests always share it (parameter order does not matter).</summary>
    public static string HashParameters(ReportRefKind kind, Guid? refId, ReportFormat format, IReadOnlyDictionary<string, string> parameters)
    {
        var canonical = $"{kind}|{refId}|{format}|" + string.Join('&', parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static ReportExport Request(Guid requestedBy, ReportRefKind kind, Guid? refId, ReportFormat format, IReadOnlyDictionary<string, string> parameters, DateTime nowUtc)
    {
        Guard.Ensure(requestedBy != Guid.Empty, ReportingRuleCodes.ExportInvalidTransition, "An export needs the requesting administrator.");
        Guard.Ensure(Enum.IsDefined(kind) && Enum.IsDefined(format), ReportingRuleCodes.ExportInvalidTransition, "Unknown report reference kind or format.");
        Guard.Ensure((kind == ReportRefKind.LaborMarket) == (refId is null), ReportingRuleCodes.ExportInvalidTransition,
            "A labor-market export has no reference id; every other kind needs one.");
        return new ReportExport
        {
            Id = Guid.NewGuid(),
            RequestedBy = requestedBy,
            RefKind = kind,
            RefId = refId,
            Format = format,
            ParametersJson = JsonSerializer.Serialize(parameters),
            ParametersHash = HashParameters(kind, refId, format, parameters),
            Status = ExportStatus.Queued,
            RequestedAtUtc = nowUtc
        };
    }

    public void StartGenerating()
    {
        Guard.Ensure(Status == ExportStatus.Queued, ReportingRuleCodes.ExportInvalidTransition, "Only a queued export can start generating.", ReportingErrorCodes.CustomForbidden,
            BusinessRuleKind.Conflict);
        Status = ExportStatus.Generating;
    }

    public void Complete(string resultRef, DateTime nowUtc)
    {
        Guard.Ensure(Status == ExportStatus.Generating, ReportingRuleCodes.ExportInvalidTransition, "Only a generating export can complete.", ReportingErrorCodes.CustomForbidden,
            BusinessRuleKind.Conflict);
        Guard.Ensure(!string.IsNullOrWhiteSpace(resultRef), ReportingRuleCodes.ExportInvalidTransition, "A result reference is required.");
        Status = ExportStatus.Ready;
        ResultRef = resultRef;
        CompletedAtUtc = nowUtc;
    }

    public void Fail(string reason, DateTime nowUtc)
    {
        Guard.Ensure(IsInProgress, ReportingRuleCodes.ExportInvalidTransition, "Only an export in progress can fail.", ReportingErrorCodes.CustomForbidden, BusinessRuleKind.Conflict);
        Status = ExportStatus.Failed;
        FailureReason = reason.Length > 500 ? reason[..500] : reason;
        CompletedAtUtc = nowUtc;
    }
}

/// <summary>The generated file of a Ready export (kept beside the export; a signed link points at it).</summary>
public sealed class ReportExportFile : Entity<Guid>
{
    private ReportExportFile()
    {
    }

    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public byte[] Content { get; private set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>The file belongs to the export: its id is the export id.</summary>
    public static ReportExportFile For(Guid exportId, string fileName, string contentType, byte[] content, DateTime nowUtc)
    {
        Guard.Ensure(content.Length > 0, ReportingRuleCodes.ExportInvalidTransition, "An export file cannot be empty.");
        return new ReportExportFile { Id = exportId, FileName = fileName, ContentType = contentType, Content = content, CreatedAtUtc = nowUtc };
    }
}
