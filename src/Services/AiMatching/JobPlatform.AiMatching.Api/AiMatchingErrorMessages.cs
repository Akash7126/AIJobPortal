namespace JobPlatform.AiMatching.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class AiMatchingErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-VBMA-FORBIDDEN"] = ("You are not allowed to perform this action or to see this data.", "غير مسموح لك بتنفيذ هذا الإجراء أو عرض هذه البيانات."),
        ["E-JRE-FORBIDDEN"] = ("Only the owner of the job posting may see its candidate recommendations.", "يمكن لصاحب الإعلان فقط عرض المرشحين الموصى بهم."),
        ["E-VBMA-INVALID-FIELD"] = ("A value is outside its allowed range: the threshold must be between 0 and 100 and the six weights must sum to 100.", "قيمة خارج النطاق المسموح: يجب أن تكون العتبة بين 0 و100 وأن يكون مجموع الأوزان الستة 100."),
        ["E-VBMA-UNSUPPORTED-FORMAT"] = ("The resume file is corrupt, unreadable or in an unsupported format.", "ملف السيرة الذاتية تالف أو غير مقروء أو بصيغة غير مدعومة."),
        ["E-VBMA-NOT-FOUND"] = ("The requested data was not found.", "لم يتم العثور على البيانات المطلوبة."),
        ["E-VBMA-PROFILE-NOT-FOUND"] = ("No profile is known for your account yet.", "لا يوجد ملف شخصي معروف لحسابك بعد."),
        ["E-VBMA-POSTING-NOT-FOUND"] = ("The job posting was not found or is not active.", "لم يتم العثور على الإعلان أو أنه غير نشط."),
        ["E-VBMA-UPSTREAM-UNAVAILABLE"] = ("A dependent service is temporarily unavailable. Please try again shortly.", "خدمة مرتبطة غير متاحة مؤقتا. يرجى المحاولة بعد قليل.")
    };
}
