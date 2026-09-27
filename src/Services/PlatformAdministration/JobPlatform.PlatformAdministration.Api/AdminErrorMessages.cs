namespace JobPlatform.PlatformAdministration.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class AdminErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-AUM-FORBIDDEN"] = ("Only administrators may perform this action.", "يمكن للمسؤولين فقط تنفيذ هذا الإجراء."),
        ["E-AUM-DUPLICATE"] = ("This entity already exists.", "هذا الكيان موجود بالفعل."),
        ["E-AUM-INVALID-FIELD"] = ("A value is invalid or outside the allowed bounds.", "قيمة غير صالحة أو خارج الحدود المسموحة."),
        ["E-AUM-STATE-INACTIVE"] = ("The job offering is already inactive.", "عرض العمل غير نشط بالفعل."),
        ["E-AUM-ENTRY-IN-USE"] = ("The entry is still referenced. Confirm explicitly to remove it.", "الإدخال ما زال مستخدما. أكد صراحة لإزالته."),
        ["E-AUM-NOT-FOUND"] = ("The requested resource was not found.", "لم يتم العثور على المورد المطلوب."),
        ["E-AUM-USERS-UNAVAILABLE"] = ("The user directory is unavailable. Try again later.", "دليل المستخدمين غير متاح. حاول لاحقا.")
    };
}
