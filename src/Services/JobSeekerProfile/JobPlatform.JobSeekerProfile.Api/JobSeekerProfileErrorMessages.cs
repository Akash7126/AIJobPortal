namespace JobPlatform.JobSeekerProfile.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class JobSeekerProfileErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-JSRPM-REQUIRED-FIELD"] = ("A required field is missing.", "حقل مطلوب مفقود."),
        ["E-JSRPM-FORBIDDEN"] = ("Only the owner may perform this action.", "يمكن للمالك فقط تنفيذ هذا الإجراء."),
        ["E-JSRPM-CONFLICT"] = ("The profile was modified since it was last read. Reload and retry.", "تم تعديل الملف الشخصي منذ آخر قراءة. أعد التحميل والمحاولة مرة أخرى."),
        ["E-JSRPM-UNSUPPORTED-FORMAT"] = ("The file format is not supported.", "صيغة الملف غير مدعومة."),
        ["E-JSRPM-TOO-LARGE"] = ("The file exceeds the 10 MB limit.", "يتجاوز الملف الحد الأقصى البالغ 10 ميغابايت."),
        ["E-JSRPM-NOT-FOUND"] = ("The requested resource was not found.", "لم يتم العثور على المورد المطلوب."),
        ["E-JSRPM-ACCOUNT-NOT-ACTIVE"] = ("The account must be an active job seeker before a profile can be created.", "يجب أن يكون الحساب لباحث عمل نشط قبل إنشاء ملف شخصي."),
        ["E-JSRPM-LEVEL1-INCOMPLETE"] = ("Complete Level 1 before adding this section.", "أكمل المستوى الأول قبل إضافة هذا القسم."),
        ["E-JSRPM-NO-RESUME"] = ("Upload a resume before this action.", "قم بتحميل السيرة الذاتية قبل هذا الإجراء."),
        ["E-JSRPM-SHARING-NOT-ACTIVATED"] = ("Activate public sharing before generating a share link.", "فعّل المشاركة العامة قبل إنشاء رابط مشاركة."),
        ["E-ERPM-FORBIDDEN"] = ("Only the owner may perform this action.", "يمكن للمالك فقط تنفيذ هذا الإجراء."),
        ["E-ERPM-TOO-LARGE"] = ("The file exceeds the 10 MB limit.", "يتجاوز الملف الحد الأقصى البالغ 10 ميغابايت."),
        ["E-ERPM-UNSUPPORTED-FORMAT"] = ("The file format is not supported.", "صيغة الملف غير مدعومة."),
        ["E-JSRPM-DUPLICATE"] = ("A profile already exists for this account.", "يوجد بالفعل ملف شخصي لهذا الحساب.")
    };
}
