using System.Security.Cryptography;
using System.Text;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain;

/// <summary>
/// Administrator report export (US-3.1.4-10): Queued, Generating, then Ready or Failed. INV-04: an identical request (same administrator, report type and
/// parameters) while one is Queued or Generating reuses that job instead of starting another (enforced by the handler through the repository and by a filtered
/// unique index as the final guard).
/// </summary>
public sealed class ExportJob : AggregateRoot<Guid>
{
    private ExportJob()
    {
    }

    public Guid RequestedBy { get; private set; }
    public ReportType ReportType { get; private set; }
    public string ParametersJson { get; private set; } = "{}";
    public string ParametersHash { get; private set; } = string.Empty;
    public ExportFormat Format { get; private set; }
    public ExportStatus Status { get; private set; }
    public string? ResultRef { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public bool IsInProgress => Status is ExportStatus.Queued or ExportStatus.Generating;

    /// <summary>Stable hash of the report type, format and parameters: two identical requests always share it.</summary>
    public static string HashParameters(ReportType type, ExportFormat format, IReadOnlyDictionary<string, string> parameters)
    {
        var canonical = $"{type}|{format}|" + string.Join('&', parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static ExportJob Request(Guid requestedBy, ReportType type, ExportFormat format, IReadOnlyDictionary<string, string> parameters, DateTime nowUtc)
    {
        Guard.Ensure(requestedBy != Guid.Empty, AuditRuleCodes.InvalidEntry, "An export needs the requesting administrator.");
        return new ExportJob
        {
            Id = Guid.NewGuid(),
            RequestedBy = requestedBy,
            ReportType = type,
            Format = format,
            ParametersJson = System.Text.Json.JsonSerializer.Serialize(parameters),
            ParametersHash = HashParameters(type, format, parameters),
            Status = ExportStatus.Queued,
            RequestedAtUtc = nowUtc
        };
    }

    public void StartGenerating()
    {
        Guard.Ensure(Status == ExportStatus.Queued, AuditRuleCodes.ExportInvalidTransition, "Only a queued export can start generating.",
            AuditErrorCodes.AdminForbidden, BusinessRuleKind.Conflict);
        Status = ExportStatus.Generating;
    }

    public void Complete(string resultRef, DateTime nowUtc)
    {
        Guard.Ensure(Status == ExportStatus.Generating, AuditRuleCodes.ExportInvalidTransition, "Only a generating export can complete.",
            AuditErrorCodes.AdminForbidden, BusinessRuleKind.Conflict);
        Guard.Ensure(!string.IsNullOrWhiteSpace(resultRef), AuditRuleCodes.InvalidEntry, "A result reference is required.");
        Status = ExportStatus.Ready;
        ResultRef = resultRef;
        CompletedAtUtc = nowUtc;
    }

    public void Fail(string reason, DateTime nowUtc)
    {
        Guard.Ensure(IsInProgress, AuditRuleCodes.ExportInvalidTransition, "Only an export in progress can fail.", AuditErrorCodes.AdminForbidden,
            BusinessRuleKind.Conflict);
        Status = ExportStatus.Failed;
        FailureReason = reason.Length > 500 ? reason[..500] : reason;
        CompletedAtUtc = nowUtc;
    }
}
