using System.Text.RegularExpressions;
using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Infrastructure.Ai;

/// <summary>
/// Deterministic local resume parser (Arabic and English, PDF/DOCX/TXT): finds section headings and contact details with rules and reports a confidence per field
/// (a found heading with content is high confidence, a keyword guess is low). Behind <see cref="IResumeParser"/> so an OCR + NLP model can replace it.
/// </summary>
public sealed partial class RuleBasedResumeParser : IResumeParser
{
    public const string Model = "rules-1";

    private static readonly (ParsedFieldName Field, string[] Headings)[] Sections =
    {
        (ParsedFieldName.EducationHistory, new[] { "education", "academic background", "qualifications", "التعليم", "المؤهلات العلمية", "المؤهلات", "الخلفية الأكاديمية" }),
        (ParsedFieldName.WorkExperience, new[] { "experience", "work experience", "employment history", "professional experience", "الخبرة", "الخبرات", "الخبرة العملية", "الخبرات العملية" }),
        (ParsedFieldName.Skills, new[] { "skills", "technical skills", "key skills", "المهارات", "مهارات" }),
        (ParsedFieldName.Certifications, new[] { "certifications", "certificates", "training", "courses", "الشهادات", "الدورات", "دورات تدريبية" }),
        (ParsedFieldName.Achievements, new[] { "achievements", "awards", "accomplishments", "الإنجازات", "الانجازات", "الجوائز" })
    };

    private static readonly string[] TitleWords =
    {
        "engineer", "developer", "manager", "accountant", "teacher", "analyst", "designer", "nurse", "technician", "مهندس", "مطور", "مدير", "محاسب", "معلم", "ممرض", "فني"
    };

    public Task<ParserResult?> ParseAsync(byte[] content, string format, CancellationToken ct = default)
    {
        var text = TextExtraction.Extract(content, format);
        return Task.FromResult(text is null ? null : ParseText(text));
    }

    public static ParserResult ParseText(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var found = new Dictionary<ParsedFieldName, List<string>>();
        ParsedFieldName? current = null;
        var preamble = new List<string>();
        foreach (var line in lines)
        {
            var heading = Sections.FirstOrDefault(s => s.Headings.Any(h => IsHeading(line, h)));
            if (heading.Headings is not null)
            {
                current = heading.Field;
                found.TryAdd(current.Value, new List<string>());
            }
            else if (current is { } section)
            {
                found[section].Add(line);
            }
            else
            {
                preamble.Add(line);
            }
        }

        var fields = new List<ParsedFieldInput>();
        var name = preamble.FirstOrDefault();
        if (name is not null)
        {
            fields.Add(new ParsedFieldInput(ParsedFieldName.PersonalDetails, name, name.Split(' ').Length <= 6 && !name.Contains('@') ? 75m : 50m));
        }

        var contact = new List<string>();
        var email = EmailPattern().Match(text);
        var phone = PhonePattern().Match(text);
        if (email.Success) { contact.Add(email.Value); }
        if (phone.Success) { contact.Add(phone.Value.Trim()); }
        if (contact.Count > 0)
        {
            fields.Add(new ParsedFieldInput(ParsedFieldName.ContactInformation, string.Join("; ", contact), email.Success && phone.Success ? 95m : 80m));
        }

        foreach (var (field, _) in Sections)
        {
            if (found.TryGetValue(field, out var body) && body.Count > 0)
            {
                fields.Add(new ParsedFieldInput(field, string.Join("\n", body), field == ParsedFieldName.Skills ? 90m : 85m));
            }
        }

        var skills = found.TryGetValue(ParsedFieldName.Skills, out var skillLines)
            ? TextNormalizer.SplitList(string.Join(",", skillLines.Select(l => l.TrimStart('-', '*', '•', '·', ' '))))
            : Array.Empty<string>();
        var experience = found.TryGetValue(ParsedFieldName.WorkExperience, out var jobLines) ? jobLines : new List<string>();
        var titles = experience.Where(l => TitleWords.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase))).Select(l => l.Length > 100 ? l[..100] : l).Distinct().Take(3).ToArray();
        var years = YearsPattern().Match(text);
        return new ParserResult(TextNormalizer.DetectLanguage(text), fields, skills, titles, years.Success ? int.Parse(years.Groups[1].Value) : null, Model);
    }

    private static bool IsHeading(string line, string heading)
    {
        var normalized = TextNormalizer.Normalize(line.TrimEnd(':', '-', ' '));
        return normalized == TextNormalizer.Normalize(heading);
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?:\+|00)?\d[\d\s\-()]{7,16}\d")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(\d{1,2})\s*\+?\s*(?:years|yrs|year|سنوات|سنة)", RegexOptions.IgnoreCase)]
    private static partial Regex YearsPattern();
}

/// <summary>Deterministic local analyser: skills come from the declared list plus taxonomy terms found in the text; levels and categories from simple patterns.</summary>
public sealed partial class KeywordSemanticAnalyzer : ISemanticAnalyzer
{
    public const string Model = "keywords-1";

    private static readonly (string Level, string[] Terms)[] Levels =
    {
        ("Entry", new[] { "entry level", "junior", "fresh graduate", "intern", "مبتدئ", "خريج جديد" }),
        ("Mid", new[] { "mid level", "intermediate", "متوسط الخبرة" }),
        ("Senior", new[] { "senior", "lead", "principal", "خبير", "كبير", "قيادي" })
    };

    public Task<SemanticAnalysis> AnalyzeAsync(string title, string? description, IReadOnlyList<string> declaredSkills, string category, SkillTaxonomy taxonomy,
        CancellationToken ct = default)
    {
        var text = $"{title} {description}";
        var normalized = " " + TextNormalizer.Normalize(text) + " ";
        var skills = new List<string>(declaredSkills);
        foreach (var term in taxonomy.Terms.Where(t => normalized.Contains(" " + t.Key + " ", StringComparison.Ordinal)))
        {
            skills.Add(term.Code);
        }

        var levels = Levels.Where(l => l.Terms.Any(t => normalized.Contains(TextNormalizer.Normalize(t)))).Select(l => l.Level).ToList();
        var years = YearsPattern().Match(text);
        if (years.Success)
        {
            levels.Add(int.Parse(years.Groups[1].Value) >= 5 ? "Senior" : "Mid");
        }

        var language = TextNormalizer.DetectLanguage(text);
        var confidence = 60m + (skills.Count > 0 ? 20m : 0m) + (description is { Length: > 40 } ? 15m : 0m);
        var categories = string.IsNullOrWhiteSpace(category) ? Array.Empty<string>() : new[] { category };
        return Task.FromResult(new SemanticAnalysis(skills, levels, categories, language, confidence, Model));
    }

    [GeneratedRegex(@"(\d{1,2})\s*\+?\s*(?:years|yrs|year|سنوات|سنة)", RegexOptions.IgnoreCase)]
    private static partial Regex YearsPattern();
}

/// <summary>No activity history exists in any BC today (Q-06), so recommendations are content-only until an event source is added.</summary>
public sealed class NoActivityHistory : IActivityHistory
{
    public Task<IReadOnlyDictionary<Guid, decimal>> GetCollaborativeScoresAsync(Guid profileId, IReadOnlyCollection<Guid> candidatePostingIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(new Dictionary<Guid, decimal>());
}
