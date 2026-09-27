namespace JobPlatform.Notification.Api;

/// <summary>Arabic and English message of every error code this service publishes (THR-004). Test-enforced.</summary>
public static class NotificationErrorMessages
{
    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Catalog = new Dictionary<string, (string En, string Ar)>
    {
        ["E-INAPPN-FORBIDDEN"] = ("You may only manage your own notifications.", "يمكنك إدارة إشعاراتك الخاصة فقط."),
        ["E-INAPPN-NOT-FOUND"] = ("The notification was not found.", "لم يتم العثور على الإشعار."),
        ["E-EMAILN-FORBIDDEN"] = ("You are not allowed to change these e-mail settings.", "غير مسموح لك بتغيير إعدادات البريد الإلكتروني هذه."),
        ["E-EMAILN-UPSTREAM-TIMEOUT"] = ("The e-mail provider did not answer in time.", "لم يستجب مزود البريد الإلكتروني في الوقت المحدد."),
        ["E-SMSN-FORBIDDEN"] = ("You are not allowed to change these SMS settings.", "غير مسموح لك بتغيير إعدادات الرسائل النصية هذه."),
        ["E-SMSN-INVALID-FIELD"] = ("The mobile number or SMS content is invalid.", "رقم الجوال أو محتوى الرسالة النصية غير صالح."),
        ["E-SMSN-UPSTREAM-TIMEOUT"] = ("The SMS gateway did not answer in time.", "لم تستجب بوابة الرسائل النصية في الوقت المحدد."),
        ["E-NOTIF-INVALID-FIELD"] = ("A submitted value is invalid.", "قيمة مدخلة غير صالحة."),
        ["E-NOTIF-MANDATORY-CATEGORY"] = ("This notification is mandatory and cannot be turned off.", "هذا الإشعار إلزامي ولا يمكن إيقافه."),
        ["E-NOTIF-TEMPLATE-NOT-FOUND"] = ("The e-mail template was not found.", "لم يتم العثور على قالب البريد الإلكتروني."),
        ["E-NOTIF-INVALID-TOKEN"] = ("The unsubscribe link is invalid.", "رابط إلغاء الاشتراك غير صالح."),
        ["E-NOTIF-NOT-FOUND"] = ("The notification was not found.", "لم يتم العثور على الإشعار."),
        ["E-NOTIF-CONTACT-UNAVAILABLE"] = ("The recipient's contact details are unavailable.", "بيانات التواصل الخاصة بالمستلم غير متاحة."),
        ["E-NOTIF-PROVIDER-REFUSED"] = ("The provider refused the message.", "رفض المزود الرسالة.")
    };
}
