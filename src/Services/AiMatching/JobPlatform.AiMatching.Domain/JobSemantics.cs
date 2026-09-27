using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

/// <summary>NLP result for one posting (before the aggregate applies the unsupported-language rule).</summary>
public sealed record SemanticAnalysis(
    IReadOnlyList<string> RequiredSkills, IReadOnlyList<string> ExperienceLevels, IReadOnlyList<string> Categories, TextLanguage Language, decimal Confidence,
    string ModelVersion);

/// <summary>
/// What the NLP extracted from a posting: required skills, experience levels, categories (handover 3.6). Identity = the posting id; it is stored here and
/// attached to the posting for matching only (BC-09 is unaffected). An unsupported language gives a best-effort, reduced-confidence, flagged result.
/// </summary>
public sealed class JobSemantics : AggregateRoot<Guid>
{
    public const decimal UnsupportedLanguageFactor = 0.75m;

    private JobSemantics()
    {
    }

    public IReadOnlyList<string> RequiredSkills { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> ExperienceLevels { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Categories { get; private set; } = Array.Empty<string>();
    public TextLanguage Language { get; private set; }
    public decimal Confidence { get; private set; }
    public bool LowConfidence { get; private set; }
    public string ModelVersion { get; private set; } = string.Empty;
    public int SemanticsVersion { get; private set; }
    public long PostingVersion { get; private set; }
    public DateTime AnalyzedAtUtc { get; private set; }

    public static JobSemantics Analyze(Guid jobPostingId, long postingVersion, SemanticAnalysis analysis, DateTime nowUtc)
    {
        var semantics = new JobSemantics { Id = jobPostingId, SemanticsVersion = 0 };
        semantics.Apply(postingVersion, analysis, nowUtc);
        return semantics;
    }

    /// <summary>Re-analysis after a posting edit. Older or equal posting versions are ignored (out-of-order safe). Returns true when the state changed.</summary>
    public bool Reanalyze(long postingVersion, SemanticAnalysis analysis, DateTime nowUtc)
    {
        if (postingVersion <= PostingVersion)
        {
            return false;
        }

        Apply(postingVersion, analysis, nowUtc);
        return true;
    }

    private void Apply(long postingVersion, SemanticAnalysis analysis, DateTime nowUtc)
    {
        var unsupported = analysis.Language == TextLanguage.Other;
        RequiredSkills = analysis.RequiredSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ExperienceLevels = analysis.ExperienceLevels.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Categories = analysis.Categories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Language = analysis.Language;
        Confidence = Math.Round(Math.Min(100m, Math.Max(0m, analysis.Confidence)) * (unsupported ? UnsupportedLanguageFactor : 1m), 2);
        LowConfidence = unsupported;
        ModelVersion = analysis.ModelVersion;
        PostingVersion = postingVersion;
        SemanticsVersion++;
        AnalyzedAtUtc = nowUtc;
    }
}
