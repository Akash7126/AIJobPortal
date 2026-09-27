namespace JobPlatform.EmployerOnboarding.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class EmployerOnboardingErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-AUM-STATE-APPROVED"] = ("This registration has already been approved.", "تمت الموافقة على هذا التسجيل بالفعل."),
        ["E-AUM-FORBIDDEN"] = ("Only administrators may perform this action.", "يمكن للمسؤولين فقط تنفيذ هذا الإجراء."),
        ["E-AUM-STATE-NOT-PENDING"] = ("Level-2 details can only be edited while the registration is pending.", "لا يمكن تعديل بيانات المستوى الثاني إلا أثناء انتظار التسجيل."),
        ["E-AUM-PROFILE-NOT-SUBMITTED"] = ("The employer has not submitted their company profile yet.", "لم يقدم صاحب العمل ملفه التعريفي بعد."),
        ["E-ERPM-TOO-LARGE"] = ("The file exceeds the 10 MB limit.", "يتجاوز الملف الحد الأقصى البالغ 10 ميجابايت."),
        ["E-ERPM-UNSUPPORTED-FORMAT"] = ("The file format is not supported.", "صيغة الملف غير مدعومة."),
        ["E-ERPM-FORBIDDEN"] = ("Only the owning employer may perform this action.", "يمكن لصاحب العمل المالك فقط تنفيذ هذا الإجراء."),
        ["E-EO-NOT-FOUND"] = ("The requested resource was not found.", "لم يتم العثور على المورد المطلوب."),
        ["E-EO-ACCOUNT-NOT-ACTIVE"] = ("The employer account is not active.", "حساب صاحب العمل غير نشط.")
    };
}
