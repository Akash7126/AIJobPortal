using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class SkillTaxonomyTests
{
    private static readonly SkillTaxonomy Taxonomy = new("v1", new[]
    {
        new TaxonomySkill("SK-CS", "C#", "سي شارب", new[] { "csharp", "dotnet c#" }),
        new TaxonomySkill("SK-SQL", "SQL", "اس كيو ال", Array.Empty<string>())
    });

    [Fact]
    public void Lookup_ExactSynonym_ReturnsFullConfidence()
    {
        var match = Taxonomy.Lookup("csharp");

        match.Should().NotBeNull();
        match!.Code.Should().Be("SK-CS");
        match.Confidence.Should().Be(100);
    }

    [Fact]
    public void Lookup_ExactLabel_ReturnsFullConfidence() =>
        Taxonomy.Lookup("SQL")!.Code.Should().Be("SK-SQL");

    [Fact]
    public void Lookup_FuzzyMisspelling_ReturnsBelowFullConfidence()
    {
        var match = Taxonomy.Lookup("csharpp");

        match.Should().NotBeNull();
        match!.Code.Should().Be("SK-CS");
        match.Confidence.Should().BeLessThan(100);
    }

    [Fact]
    public void Lookup_NoMatch_ReturnsNull() =>
        Taxonomy.Lookup("kubernetes").Should().BeNull();

    [Fact]
    public void Lookup_Empty_ReturnsNull() =>
        Taxonomy.Lookup("").Should().BeNull();

    [Fact]
    public void Count_DistinctCodes() =>
        Taxonomy.Count.Should().Be(2);
}

public class SkillStandardizationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static readonly SkillTaxonomy TaxonomyV1 = new("v1", new[] { new TaxonomySkill("SK-CS", "C#", "سي شارب", Array.Empty<string>()) });
    private static readonly SkillTaxonomy TaxonomyV2 = new("v2", new[]
    {
        new TaxonomySkill("SK-CS", "C#", "سي شارب", Array.Empty<string>()),
        new TaxonomySkill("SK-PY", "Python", "بايثون", Array.Empty<string>())
    });

    [Fact]
    public void Standardize_KnownTerm_MapsToCanonicalCode()
    {
        var run = SkillStandardization.Standardize(Guid.NewGuid(), Guid.NewGuid(), new[] { "C#" }, TaxonomyV1, At);

        run.Mappings.Single().CanonicalCode.Should().Be("SK-CS");
        run.Mappings.Single().IsFreeText.Should().BeFalse();
        run.Status.Should().Be(StandardizationStatus.Standardized);
    }

    [Fact]
    public void Standardize_UnknownTerm_KeptAsFreeTextAndFlaggedForReview()
    {
        var run = SkillStandardization.Standardize(Guid.NewGuid(), Guid.NewGuid(), new[] { "Quantum Basket Weaving" }, TaxonomyV1, At);

        run.Mappings.Single().IsFreeText.Should().BeTrue();
        run.Mappings.Single().NeedsReview.Should().BeTrue();
        run.Mappings.Single().ExtractedTerm.Should().Be("Quantum Basket Weaving");
        run.Status.Should().Be(StandardizationStatus.NeedsReview);
    }

    [Fact]
    public void Standardize_DuplicateNormalisedTerms_KeptOnce()
    {
        var run = SkillStandardization.Standardize(Guid.NewGuid(), Guid.NewGuid(), new[] { "C#", " c# ", "C#" }, TaxonomyV1, At);

        run.Mappings.Should().ContainSingle();
    }

    [Fact]
    public void Standardize_CapturesTaxonomyVersionAndRaisesEvent()
    {
        var profileId = Guid.NewGuid();

        var run = SkillStandardization.Standardize(Guid.NewGuid(), profileId, new[] { "C#" }, TaxonomyV1, At);

        run.TaxonomyVersion.Should().Be("v1");
        run.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SkillStandardizationUpdatedDomainEvent>()
            .Which.ProfileId.Should().Be(profileId);
    }

    [Fact]
    public void Restandardize_SameVersion_ReturnsFalseAndDoesNotChange()
    {
        var run = SkillStandardization.Standardize(Guid.NewGuid(), Guid.NewGuid(), new[] { "C#" }, TaxonomyV1, At);

        var changed = run.Restandardize(TaxonomyV1, At.AddHours(1));

        changed.Should().BeFalse();
    }

    [Fact]
    public void Restandardize_NewerVersion_RemapsAgainstNewTaxonomy()
    {
        var run = SkillStandardization.Standardize(Guid.NewGuid(), Guid.NewGuid(), new[] { "Python" }, TaxonomyV1, At);
        run.Mappings.Single().IsFreeText.Should().BeTrue(); // not in v1

        var changed = run.Restandardize(TaxonomyV2, At.AddHours(1));

        changed.Should().BeTrue();
        run.TaxonomyVersion.Should().Be("v2");
        run.Mappings.Single().CanonicalCode.Should().Be("SK-PY");
    }

    [Fact]
    public void Restandardize_RunningRunKeepsOldVersion_UntilExplicitlyRestandardized()
    {
        var run = SkillStandardization.Standardize(Guid.NewGuid(), Guid.NewGuid(), new[] { "C#" }, TaxonomyV1, At);

        run.TaxonomyVersion.Should().Be("v1");
    }
}
