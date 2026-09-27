using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.Common;

/// <summary>Externally published error codes of BC-06 (handover section 4). Codes not catalogued by the handover for a given rule are
/// surfaced as the internal rule code itself (foundation convention: <see cref="BusinessRule"/> with no ExternalCode falls back to Code).</summary>
public static class ErrorCodes
{
    public const string NewsRequiredField = "E-NEWSU-REQUIRED-FIELD";
    public const string NewsTooLarge = "E-NEWSU-TOO-LARGE";
    public const string NewsUnsupportedFormat = "E-NEWSU-UNSUPPORTED-FORMAT";
    public const string NewsForbidden = "E-NEWSU-FORBIDDEN";

    public const string HelpRequiredField = "E-FAQHC-REQUIRED-FIELD";
    public const string HelpUnsupportedFormat = "E-FAQHC-UNSUPPORTED-FORMAT";
    public const string HelpForbidden = "E-FAQHC-FORBIDDEN";

    /// <summary>Added: the company profile page is not part of either the News or Help/FAQ error families (handover section 3.6).</summary>
    public const string CompanyPageForbidden = "E-HCCP-FORBIDDEN";

    /// <summary>Added: 404 of any BC-06 resource.</summary>
    public const string NotFound = "E-HC-NOT-FOUND";
}

/// <summary>Internal rule codes (foundation section 7 format HC.Aggregate.RULE).</summary>
public static class RuleCodes
{
    public const string NewsRequiredField = "HC.News.REQUIRED_FIELD";
    public const string NewsMustBeDraftToPublish = "HC.News.MUST_BE_DRAFT_TO_PUBLISH";
    public const string NewsMustBePublishedToArchive = "HC.News.MUST_BE_PUBLISHED_TO_ARCHIVE";
    public const string NewsMediaTooLarge = "HC.News.MEDIA_TOO_LARGE";
    public const string NewsMediaUnsupportedFormat = "HC.News.MEDIA_UNSUPPORTED_FORMAT";
    public const string NewsMediaNoAltText = "HC.News.MEDIA_NO_ALT_TEXT";
    public const string NewsAdminOnly = "HC.News.ADMIN_ONLY";
    public const string NewsEditOnlyDraft = "HC.News.EDIT_ONLY_DRAFT";

    public const string CategorizationAdminOnly = "HC.ContentCategorization.ADMIN_ONLY";

    public const string HelpRequiredField = "HC.Help.REQUIRED_FIELD";
    public const string HelpUnsupportedFormat = "HC.Help.UNSUPPORTED_FORMAT";
    public const string HelpNoCaptions = "HC.Help.NO_CAPTIONS";
    public const string HelpAdminOnly = "HC.Help.ADMIN_ONLY";

    public const string OrganizationAdminOnly = "HC.HelpContentOrganization.ADMIN_ONLY";

    public const string FeedbackContentNotFound = "HC.Feedback.CONTENT_NOT_FOUND";

    public const string CompanyPageOwnerOrAdminOnly = "HC.CompanyPage.OWNER_OR_ADMIN_ONLY";

    public const string ContextHelpMappingAdminOnly = "HC.ContextHelpMapping.ADMIN_ONLY";

    public const string TutorialAdminOnly = "HC.Tutorial.ADMIN_ONLY";
}

/// <summary>The caller as the domain needs to see it (no framework types): who, and whether an administrator.</summary>
public readonly record struct Actor(Guid Id, bool IsAdministrator);

internal static class Rules
{
    public static IBusinessRule AdminOnly(Actor actor, string ruleCode, string externalCode) =>
        new BusinessRule(ruleCode, "Only administrators may perform this action.", !actor.IsAdministrator, externalCode, BusinessRuleKind.Forbidden);

    /// <summary>Owner-or-administrator refusal for a self-service resource the aggregate itself is not part of a role hierarchy for
    /// (handover section 3.6: "only the employer (owner) or an administrator").</summary>
    public static IBusinessRule OwnerOrAdminOnly(Actor actor, Guid ownerId, string ruleCode, string externalCode) =>
        new BusinessRule(ruleCode, "Only the owning employer or an administrator may perform this action.",
            actor.Id != ownerId && !actor.IsAdministrator, externalCode, BusinessRuleKind.Forbidden);
}

/// <summary>Bilingual text (ar/en - THR-075). At least one language must be supplied for a required field; a reader falls back to the other
/// language when the requested one is empty (handover section 3.1 "served by locale with fallback").</summary>
public sealed class LocalizedText : ValueObject
{
    public LocalizedText(string? ar, string? en)
    {
        Ar = ar;
        En = en;
    }

    public string? Ar { get; }
    public string? En { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Ar) && string.IsNullOrWhiteSpace(En);

    /// <summary>The requested language, falling back to the other when empty.</summary>
    public string? For(bool arabic) => arabic ? (string.IsNullOrWhiteSpace(Ar) ? En : Ar) : (string.IsNullOrWhiteSpace(En) ? Ar : En);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Ar;
        yield return En;
    }
}
