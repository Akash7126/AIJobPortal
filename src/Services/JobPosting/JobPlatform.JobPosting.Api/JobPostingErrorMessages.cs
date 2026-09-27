namespace JobPlatform.JobPosting.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class JobPostingErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-JCP-REQUIRED-FIELD"] = ("Title, summary and at least one skill are required.", "العنوان والملخص ومهارة واحدة على الأقل مطلوبة."),
        ["E-JCP-INVALID-FIELD"] = ("A value does not conform to the job posting schema.", "قيمة لا تتوافق مع مخطط الإعلان الوظيفي."),
        ["E-JCP-UPSTREAM-TIMEOUT"] = ("The taxonomy service did not respond in time. Try again shortly.", "لم تستجب خدمة التصنيف في الوقت المحدد. حاول مرة أخرى قريبا."),
        ["E-JCP-FORBIDDEN"] = ("Only the employer that owns this posting may perform this action.", "يمكن فقط لصاحب العمل المالك لهذا الإعلان تنفيذ هذا الإجراء."),
        ["E-JCP-STATE-ACTIVE"] = ("Only an expired posting can be renewed.", "يمكن تجديد الإعلانات منتهية الصلاحية فقط."),
        ["E-JST-FORBIDDEN"] = ("Only the employer that owns this posting may change its status.", "يمكن فقط لصاحب العمل المالك لهذا الإعلان تغيير حالته."),
        ["E-JST-STATE-ARCHIVED"] = ("An archived posting cannot change state; recreate it instead.", "لا يمكن تغيير حالة إعلان مؤرشف؛ يجب إعادة إنشائه."),
        ["E-JST-ADMIN-SUSPENDED"] = ("An administrator has suspended this posting.", "قام أحد المسؤولين بتعليق هذا الإعلان."),
        ["E-JST-INVALID-TRANSITION"] = ("This status change is not allowed from the posting's current state.", "لا يُسمح بتغيير الحالة هذا من الحالة الحالية للإعلان."),
        ["E-JSF-INVALID-FIELD"] = ("The salary range is invalid (minimum is greater than maximum).", "نطاق الراتب غير صالح (الحد الأدنى أكبر من الحد الأقصى)."),
        ["E-JSF-FORBIDDEN"] = ("Only the owner may manage this favourite or saved search.", "يمكن فقط للمالك إدارة هذا المفضل أو البحث المحفوظ."),
        ["E-JIP-FORBIDDEN"] = ("Only the owner may manage this interested-list entry.", "يمكن فقط للمالك إدارة عنصر قائمة الاهتمام هذا."),
        ["E-JCP-NOT-FOUND"] = ("The requested job posting was not found.", "لم يتم العثور على الإعلان الوظيفي المطلوب.")
    };
}
