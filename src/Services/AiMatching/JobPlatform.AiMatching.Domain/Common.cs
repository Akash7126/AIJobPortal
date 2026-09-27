using System.Globalization;
using System.Text;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

/// <summary>The six scored criteria (GAP-002: only these are scored).</summary>
public enum Criterion
{
    SkillOverlap,
    Education,
    Training,
    Location,
    Experience,
    Salary
}

/// <summary>Ordinal education levels: the distance between levels is what the education criterion measures.</summary>
public enum EducationLevel
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Diploma = 3,
    Bachelor = 4,
    Master = 5,
    Doctorate = 6
}

public enum WorkArrangement
{
    OnSite,
    Remote,
    Hybrid
}

/// <summary>Language of a document. <see cref="Other"/> is unsupported: parsing is best effort with reduced confidence (US-3.3.1-09 AC-03).</summary>
public enum TextLanguage
{
    Ar,
    En,
    Other
}

public enum ParsedFieldName
{
    PersonalDetails,
    ContactInformation,
    EducationHistory,
    WorkExperience,
    Skills,
    Certifications,
    Achievements
}

public enum FieldSource
{
    Parser,
    User
}

public enum ParseStatus
{
    Parsed,
    Failed
}

public enum ReviewStatus
{
    Parsed,
    Reviewed
}

public enum StandardizationStatus
{
    None,
    Standardized,
    NeedsReview
}

public enum RecommendationStrategy
{
    Hybrid,
    ContentOnly
}

public enum RecommendationReason
{
    Content,
    Collaborative,
    Preference
}

public enum ShortlistStatus
{
    Queued,
    Ready,
    Failed
}

public enum KnownStanding
{
    Active,
    Deactivated
}

/// <summary>Who is acting (from the validated token). Aggregates decide what an actor may do.</summary>
public sealed record Actor(Guid Id, ActorType Type)
{
    public static readonly Guid SystemId = new("00000000-0000-0000-0000-00000000a110");

    public static Actor System { get; } = new(SystemId, ActorType.System);

    public bool IsAdministrator => Type == ActorType.Administrator;
}

/// <summary>Externally published error codes of BC-10 (handover section 4).</summary>
public static class AiErrorCodes
{
    public const string InvalidField = "E-VBMA-INVALID-FIELD";
    public const string Forbidden = "E-VBMA-FORBIDDEN";
    public const string UnsupportedFormat = "E-VBMA-UNSUPPORTED-FORMAT";
    public const string CandidateRecommendationForbidden = "E-JRE-FORBIDDEN";
    public const string NotFound = "E-VBMA-NOT-FOUND";
    public const string ProfileUnknown = "E-VBMA-PROFILE-NOT-FOUND";
    public const string PostingNotFound = "E-VBMA-POSTING-NOT-FOUND";
    public const string UpstreamUnavailable = "E-VBMA-UPSTREAM-UNAVAILABLE";
}

/// <summary>Internal rule codes, format BC.Aggregate.RULE (foundation section 7).</summary>
public static class AiRuleCodes
{
    public const string ConfigOutOfRange = "AM.Config.OUT_OF_RANGE";
    public const string ConfigAdminOnly = "AM.Config.ADMIN_ONLY";
    public const string NonActivePosting = "AM.Score.NON_ACTIVE_POSTING";
    public const string ResumeUnreadable = "AM.Resume.UNREADABLE";
    public const string ParsedNotOwner = "AM.Parsed.NOT_OWNER";
    public const string ParsedInvalidField = "AM.Parsed.INVALID_FIELD";
    public const string ShortlistNotOwner = "AM.Shortlist.NOT_OWNER";
    public const string ShortlistInvalidTransition = "AM.Shortlist.INVALID_TRANSITION";
    public const string InvalidInput = "AM.Common.INVALID_INPUT";
}

internal static class Guard
{
    public static void Ensure(bool condition, string code, string message, string? externalCode = null, BusinessRuleKind kind = BusinessRuleKind.BusinessRule)
    {
        if (!condition)
        {
            throw new BusinessRuleViolationException(code, message, externalCode, kind);
        }
    }
}

/// <summary>Normalisation shared by skill matching: case, whitespace, Arabic diacritics/tatweel and letter variants are folded so "Python", " python " and Arabic spellings compare equal.</summary>
public static class TextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousSpace = false;
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark || ch == 'ـ')
            {
                continue; // diacritics (incl. Arabic tashkeel) and tatweel
            }

            var folded = ch switch
            {
                'أ' or 'إ' or 'آ' => 'ا', // alef variants
                'ى' => 'ي',                           // alef maksura -> yeh
                'ة' => 'ه',                           // teh marbuta -> heh
                _ => char.ToLowerInvariant(ch)
            };
            if (char.IsWhiteSpace(folded) || folded == '_' || folded == '-')
            {
                if (!previousSpace)
                {
                    builder.Append(' ');
                }

                previousSpace = true;
                continue;
            }

            previousSpace = false;
            builder.Append(folded);
        }

        return builder.ToString().Trim();
    }

    /// <summary>Splits a free-form list (comma, semicolon, pipe, new line, Arabic comma) into distinct trimmed items.</summary>
    public static IReadOnlyList<string> SplitList(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(new[] { ',', ';', '|', '\n', '\r', '،', '؛' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>Detects the dominant script: Arabic, Latin (English) or neither (Other).</summary>
    public static TextLanguage DetectLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TextLanguage.Other;
        }

        int arabic = 0, latin = 0, other = 0;
        foreach (var ch in text)
        {
            if (!char.IsLetter(ch))
            {
                continue;
            }

            if (ch is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ݿ')
            {
                arabic++;
            }
            else if (ch <= 'ɏ')
            {
                latin++;
            }
            else
            {
                other++;
            }
        }

        var total = arabic + latin + other;
        if (total == 0)
        {
            return TextLanguage.Other;
        }

        if (arabic * 2 >= total && arabic >= latin)
        {
            return TextLanguage.Ar;
        }

        return latin * 2 >= total ? TextLanguage.En : TextLanguage.Other;
    }
}
