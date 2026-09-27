namespace JobPlatform.Reporting.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class ReportingErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-UAM-FORBIDDEN"] = ("Only administrators may view user activity.", "يمكن للمسؤولين فقط عرض نشاط المستخدمين."),
        ["E-UAM-INVALID-FIELD"] = ("The retention period is below the legal minimum or out of range.", "فترة الاحتفاظ أقل من الحد القانوني الأدنى أو خارج النطاق."),
        ["E-EMPST-FORBIDDEN"] = ("Only administrators may view employment statistics.", "يمكن للمسؤولين فقط عرض إحصاءات التوظيف."),
        ["E-EMPST-INVALID-FIELD"] = ("The report period is invalid: use a calendar month (yyyy-MM) that is not in the future.", "فترة التقرير غير صالحة: استخدم شهرا تقويميا (yyyy-MM) ليس في المستقبل."),
        ["E-SPM-FORBIDDEN"] = ("Only administrators may view system performance.", "يمكن للمسؤولين فقط عرض أداء النظام."),
        ["E-SPM-INVALID-FIELD"] = ("The alert rule is invalid: known metric, a numeric threshold and a window of at least one minute are required.", "قاعدة التنبيه غير صالحة: يلزم مقياس معروف وحد رقمي ونافذة لا تقل عن دقيقة."),
        ["E-CRG-FORBIDDEN"] = ("You may not use this report or report category.", "لا يمكنك استخدام هذا التقرير أو فئة التقارير."),
        ["E-CRG-INVALID-FIELD"] = ("The report definition, template or schedule is invalid.", "تعريف التقرير أو القالب أو الجدولة غير صالح."),
        ["E-CRG-UPSTREAM-TIMEOUT"] = ("Power BI did not respond in time; the built-in view was returned instead.", "لم يستجب Power BI في الوقت المناسب؛ تم عرض العرض المدمج بدلا منه."),
        ["E-CRG-NOT-FOUND"] = ("The report object was not found.", "لم يتم العثور على عنصر التقرير."),
        ["E-CRG-EXPORT-DUPLICATE"] = ("An identical export is already being created. Try again to receive it.", "يوجد تصدير مطابق قيد الإنشاء. حاول مجددا للحصول عليه."),
        ["E-CRG-EXPORT-NOT-READY"] = ("The export is not ready yet.", "التصدير ليس جاهزا بعد."),
        ["E-CRG-LINK-INVALID"] = ("The link is invalid or has expired.", "الرابط غير صالح أو انتهت صلاحيته."),
        ["E-EMPST-DUPLICATE"] = ("A report for this period already exists.", "يوجد تقرير لهذه الفترة بالفعل.")
    };
}
