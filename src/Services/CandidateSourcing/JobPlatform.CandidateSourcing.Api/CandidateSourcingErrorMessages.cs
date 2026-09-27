namespace JobPlatform.CandidateSourcing.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class CandidateSourcingErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-CRFE-FORBIDDEN"] = ("Only the owning employer may perform this action.", "يمكن لصاحب العمل المالك فقط تنفيذ هذا الإجراء."),
        ["E-CRFE-INVALID-FIELD"] = ("A value is invalid or the search filters are contradictory.", "قيمة غير صالحة أو مرشحات بحث متناقضة."),
        ["E-CRFE-NOT-FOUND"] = ("The requested resource was not found.", "لم يتم العثور على المورد المطلوب.")
    };
}
