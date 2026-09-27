namespace JobPlatform.HelpContent.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class HelpContentErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-NEWSU-REQUIRED-FIELD"] = ("Title and body are required.", "العنوان والمحتوى مطلوبان."),
        ["E-NEWSU-TOO-LARGE"] = ("The file exceeds the 10 MB limit.", "يتجاوز الملف الحد الأقصى البالغ 10 ميجابايت."),
        ["E-NEWSU-UNSUPPORTED-FORMAT"] = ("The media type or format is not supported.", "نوع أو صيغة الوسائط غير مدعومة."),
        ["E-NEWSU-FORBIDDEN"] = ("Only administrators may perform this action.", "يمكن للمسؤولين فقط تنفيذ هذا الإجراء."),
        ["E-FAQHC-REQUIRED-FIELD"] = ("Title and body are required.", "العنوان والمحتوى مطلوبان."),
        ["E-FAQHC-UNSUPPORTED-FORMAT"] = ("The media type or format is not supported.", "نوع أو صيغة الوسائط غير مدعومة."),
        ["E-FAQHC-FORBIDDEN"] = ("Only administrators may perform this action.", "يمكن للمسؤولين فقط تنفيذ هذا الإجراء."),
        ["E-HCCP-FORBIDDEN"] = ("Only the owning employer or an administrator may perform this action.", "يمكن لصاحب العمل المالك أو المسؤول فقط تنفيذ هذا الإجراء."),
        ["E-HC-NOT-FOUND"] = ("The requested resource was not found.", "لم يتم العثور على المورد المطلوب.")
    };
}
