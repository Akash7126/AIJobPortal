namespace JobPlatform.AuditLogging.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class AuditErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-TPJPRI-FORBIDDEN"] = ("You may only view your own integration logs.", "يمكنك عرض سجلات التكامل الخاصة بك فقط."),
        ["E-TPJPRI-INVALID-FIELD"] = ("The date range is invalid: the end date must not be before the start date and the range may not exceed 12 months.", "نطاق التاريخ غير صالح: يجب ألا يسبق تاريخ الانتهاء تاريخ البدء وألا يتجاوز النطاق 12 شهرا."),
        ["E-AUM-FORBIDDEN"] = ("Only administrators may view this log.", "يمكن للمسؤولين فقط عرض هذا السجل."),
        ["E-AAFR-FORBIDDEN"] = ("Only administrators may view the access log.", "يمكن للمسؤولين فقط عرض سجل الوصول."),
        ["E-GDI-FORBIDDEN"] = ("Only administrators may view the government data audit trail.", "يمكن للمسؤولين فقط عرض سجل تدقيق البيانات الحكومية."),
        ["E-EMAILN-FORBIDDEN"] = ("Only administrators may view the e-mail log.", "يمكن للمسؤولين فقط عرض سجل البريد الإلكتروني."),
        ["E-SMSN-FORBIDDEN"] = ("Only administrators may view the SMS log.", "يمكن للمسؤولين فقط عرض سجل الرسائل النصية."),
        ["E-INAPPN-FORBIDDEN"] = ("You may only view your own notification history.", "يمكنك عرض سجل الإشعارات الخاص بك فقط."),
        ["E-JST-FORBIDDEN"] = ("You may only view the status history of your own job postings.", "يمكنك عرض سجل حالة إعلانات الوظائف الخاصة بك فقط."),
        ["E-AUDIT-EMPLOYER-FORBIDDEN"] = ("Only employers may view an employer dashboard, and only their own.", "يمكن لأصحاب العمل فقط عرض لوحة صاحب العمل، ولوحتهم فقط."),
        ["E-AUDIT-INSIGHT-FORBIDDEN"] = ("You may only view insight about candidates of your own job postings.", "يمكنك عرض معلومات المرشحين لإعلاناتك فقط."),
        ["E-AUDIT-EXPORT-NOT-FOUND"] = ("The export job was not found.", "لم يتم العثور على مهمة التصدير."),
        ["E-AUDIT-INSIGHT-NOT-FOUND"] = ("No insight is available for this candidate and posting.", "لا تتوفر معلومات لهذا المرشح والإعلان."),
        ["E-AUDIT-INVALID-FIELD"] = ("The entry contains personal data or secrets and was refused.", "يحتوي السجل على بيانات شخصية أو أسرار وتم رفضه."),
        ["E-AUDIT-EXPORT-DUPLICATE"] = ("An identical export is already being created. Try again to receive it.", "يوجد تصدير مطابق قيد الإنشاء. حاول مجددا للحصول عليه."),
        ["E-AUDIT-REPORT-SOURCE-UNAVAILABLE"] = ("The report source is unavailable.", "مصدر التقارير غير متاح.")
    };
}
