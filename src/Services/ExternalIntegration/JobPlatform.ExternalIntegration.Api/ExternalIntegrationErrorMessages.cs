namespace JobPlatform.ExternalIntegration.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class ExternalIntegrationErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-EJSI-FORBIDDEN"] = ("This action requires MoL/PEF administrator approval.", "يتطلب هذا الإجراء موافقة إدارية من وزارة العمل/الصندوق الفلسطيني للتشغيل."),
        ["E-EJSI-RUN-IN-PROGRESS"] = ("A sync run is already in progress for this integration.", "تتم بالفعل مزامنة قيد التنفيذ لهذا التكامل."),
        ["E-EJSI-UPSTREAM-TIMEOUT"] = ("The partner feed did not respond in time.", "لم يستجب موجز الشريك في الوقت المحدد."),
        ["E-EJSI-NOT-ACTIVE"] = ("This action requires an active integration.", "يتطلب هذا الإجراء تكاملاً نشطًا."),
        ["E-EJSI-ALREADY-REGISTERED"] = ("This partner account already has an integration registered.", "يمتلك حساب الشريك هذا تكاملاً مسجلاً بالفعل."),
        ["E-EJSI-INVALID-FIELD"] = ("The source payload does not conform to the configured mapping.", "لا تتوافق بيانات المصدر مع التخطيط المُعد."),
        ["E-TPJPRI-FORBIDDEN"] = ("Only the owning partner may perform this action.", "يمكن للشريك المالك فقط تنفيذ هذا الإجراء."),
        ["E-TPJPRI-UPSTREAM-TIMEOUT"] = ("The upstream service did not respond in time.", "لم تستجب الخدمة الأساسية في الوقت المحدد."),
        ["E-TPJPRI-INVALID-FIELD"] = ("Title, summary and at least one skill are required.", "العنوان والملخص ومهارة واحدة على الأقل مطلوبة."),
        ["E-TPJPRI-STATE-CLOSED"] = ("This post can no longer be edited by its source.", "لم يعد بالإمكان تعديل هذا الإعلان من قبل مصدره."),
        ["E-TPJPRI-REQUIRED-FIELD"] = ("Every required standard-schema field must have a source mapping.", "يجب أن يكون لكل حقل مطلوب في المخطط القياسي تخطيط مصدر."),
        ["E-APIF-UNSUPPORTED-FORMAT"] = ("The requested data format is not supported.", "تنسيق البيانات المطلوب غير مدعوم."),
        ["E-APIF-EXPIRED"] = ("The access token has expired.", "انتهت صلاحية رمز الوصول."),
        ["E-APIF-RATE-LIMITED"] = ("Too many requests. Try again later.", "عدد كبير جدًا من الطلبات. حاول مرة أخرى لاحقًا."),
        ["E-APIF-ADMIN-ONLY"] = ("Only administrators may perform this action.", "يمكن للمسؤولين فقط تنفيذ هذا الإجراء."),
        ["E-APIF-RETIRE-BEFORE-SUNSET"] = ("This API version cannot be retired before its sunset date.", "لا يمكن إيقاف نسخة الواجهة هذه قبل تاريخ انتهائها."),
        ["E-APIF-INVALID-TRANSITION"] = ("This API version state transition is not allowed.", "انتقال حالة نسخة الواجهة هذا غير مسموح به."),
        ["E-SI-UPSTREAM-TIMEOUT"] = ("The upstream software interface did not respond in time.", "لم تستجب واجهة البرنامج الأساسية في الوقت المحدد."),
        ["E-SI-FORBIDDEN"] = ("Only platform operators may perform this action.", "يمكن لمشغلي المنصة فقط تنفيذ هذا الإجراء."),
        ["E-EI-NOT-FOUND"] = ("The requested resource was not found.", "لم يتم العثور على المورد المطلوب.")
    };
}
