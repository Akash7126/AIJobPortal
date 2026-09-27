using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class JobSemanticsTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static SemanticAnalysis Analysis(TextLanguage language = TextLanguage.En, decimal confidence = 90) =>
        new(new[] { "C#", "c#", "SQL" }, new[] { "Mid", "mid" }, new[] { "IT", "it" }, language, confidence, "model-1");

    [Fact]
    public void Analyze_SetsFieldsAndDedupesCaseInsensitively()
    {
        var semantics = JobSemantics.Analyze(Guid.NewGuid(), 1, Analysis(), At);

        semantics.RequiredSkills.Should().HaveCount(2);
        semantics.ExperienceLevels.Should().HaveCount(1);
        semantics.Categories.Should().HaveCount(1);
        semantics.Confidence.Should().Be(90);
        semantics.LowConfidence.Should().BeFalse();
        semantics.SemanticsVersion.Should().Be(1);
        semantics.PostingVersion.Should().Be(1);
    }

    [Fact]
    public void Analyze_UnsupportedLanguage_ReducesConfidenceAndFlags()
    {
        var semantics = JobSemantics.Analyze(Guid.NewGuid(), 1, Analysis(TextLanguage.Other, 100), At);

        semantics.LowConfidence.Should().BeTrue();
        semantics.Confidence.Should().Be(100 * JobSemantics.UnsupportedLanguageFactor);
    }

    [Fact]
    public void Reanalyze_NewerPostingVersion_AppliesAndBumpsSemanticsVersion()
    {
        var semantics = JobSemantics.Analyze(Guid.NewGuid(), 1, Analysis(), At);

        var changed = semantics.Reanalyze(2, Analysis(confidence: 70), At.AddHours(1));

        changed.Should().BeTrue();
        semantics.PostingVersion.Should().Be(2);
        semantics.SemanticsVersion.Should().Be(2);
        semantics.Confidence.Should().Be(70);
    }

    [Fact]
    public void Reanalyze_OlderOrEqualPostingVersion_IsIgnored()
    {
        var semantics = JobSemantics.Analyze(Guid.NewGuid(), 5, Analysis(), At);

        var sameVersion = semantics.Reanalyze(5, Analysis(confidence: 10), At.AddHours(1));
        var olderVersion = semantics.Reanalyze(3, Analysis(confidence: 5), At.AddHours(2));

        sameVersion.Should().BeFalse();
        olderVersion.Should().BeFalse();
        semantics.Confidence.Should().Be(90);
        semantics.SemanticsVersion.Should().Be(1);
    }
}
