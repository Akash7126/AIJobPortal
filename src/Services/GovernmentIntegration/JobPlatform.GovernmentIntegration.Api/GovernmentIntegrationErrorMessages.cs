namespace JobPlatform.GovernmentIntegration.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004, foundation section 7). Test-enforced.</summary>
public static class GovernmentIntegrationErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-GI-FORBIDDEN"] = ("You are not allowed to perform this action.", "غير مسموح لك بتنفيذ هذا الإجراء."),
        ["E-GI-NOT-FOUND"] = ("The requested resource was not found.", "لم يتم العثور على المورد المطلوب."),
        ["E-GI-SUBMISSION-INCOMPLETE"] = ("Registration number, VAT number and mobile number are all required.", "رقم التسجيل ورقم ضريبة القيمة المضافة ورقم الهاتف المحمول جميعها مطلوبة."),
        ["E-GI-ACCOUNT-NOT-EMPLOYER"] = ("The employer account is not yet known to Government Integration. Try again shortly.", "حساب صاحب العمل غير معروف بعد لدى خدمة التكامل الحكومي. حاول مجددا بعد قليل."),
        ["E-GI-ALREADY-DECIDED"] = ("This verification has already been decided.", "تم اتخاذ قرار بشأن هذا التحقق بالفعل."),
        ["E-GI-NOT-IN-MANUAL-REVIEW"] = ("This decision requires the verification to be pending manual review.", "يتطلب هذا القرار أن يكون التحقق قيد المراجعة اليدوية."),
        ["E-ERPM-UPSTREAM-TIMEOUT"] = ("The automatic verification call timed out.", "انتهت مهلة استدعاء التحقق التلقائي."),
        ["E-GI-SUBJECT-REQUIRED"] = ("An import must reference the subject it verifies.", "يجب أن يشير الاستيراد إلى الشخص الذي يتحقق منه."),
        ["E-GI-NOT-REQUESTED"] = ("Only a requested import can be resolved.", "يمكن فقط حل استيراد تم طلبه."),
        ["E-GDI-UPSTREAM-TIMEOUT"] = ("The government data source timed out.", "انتهت مهلة مصدر البيانات الحكومية."),
        ["E-GDI-FORBIDDEN"] = ("This component is not authorised to access government data for this purpose.", "هذا المكون غير مخول بالوصول إلى البيانات الحكومية لهذا الغرض."),
        ["E-CONSTR-UPSTREAM-TIMEOUT"] = ("The MoL/PEF reconciliation call timed out.", "انتهت مهلة استدعاء المطابقة مع وزارة العمل / صندوق التشغيل."),
        ["E-GI-NO-ACTIVE-MIGRATION"] = ("There is no active migration run to act on.", "لا يوجد تشغيل ترحيل نشط للتصرف بشأنه."),
        ["E-GI-MIGRATION-ALREADY-ACTIVE"] = ("A migration run is already active.", "يوجد بالفعل تشغيل ترحيل نشط."),
        ["E-GI-PHASE-TEST-FAILED"] = ("The migration phase test failed.", "فشل اختبار مرحلة الترحيل."),
        ["E-GI-DUPLICATE-SOURCE-RECORD"] = ("This legacy record has already been imported.", "تم استيراد هذا السجل القديم بالفعل."),
        ["E-GI-NOT-FULLY-MAPPED"] = ("The record must be mapped, transformed and validated before it can be migrated.", "يجب تعيين السجل وتحويله والتحقق منه قبل ترحيله."),
        ["E-DMIG-INVALID-FIELD"] = ("This legacy record failed validation.", "فشل هذا السجل القديم في التحقق من الصحة."),
        ["E-GI-SNAPSHOT-ISOLATION"] = ("A cleansing run is already active for this batch.", "يوجد بالفعل تشغيل تنقية نشط لهذه الدفعة."),
        ["E-DMIG-UPSTREAM-TIMEOUT"] = ("The legacy source read timed out.", "انتهت مهلة قراءة المصدر القديم.")
    };
}
