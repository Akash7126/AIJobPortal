using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.UnitTests;

public class TaxonomyTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static TaxonomyChange Add(string code, string? parent = null) =>
        new(TaxonomyChangeKind.Add, code, new LocalizedText(code + "-ar", code + "-en"), parent, null, null);

    private static PlatformTaxonomy Create(params TaxonomyChange[] initial)
    {
        var taxonomy = PlatformTaxonomy.Create("skills", At);
        if (initial.Length > 0)
        {
            taxonomy.ApplyChanges(initial, Admin, At);
        }

        taxonomy.ClearDomainEvents();
        return taxonomy;
    }

    private static Action Apply(PlatformTaxonomy taxonomy, params TaxonomyChange[] changes) => () => taxonomy.ApplyChanges(changes, Admin, At);

    [Fact]
    public void Create_StartsAtVersionOneAndNormalisesType()
    {
        var taxonomy = PlatformTaxonomy.Create(" Skills ", At);

        taxonomy.Type.Should().Be("skills");
        taxonomy.TaxonomyVersion.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1skills")]
    [InlineData("has space")]
    [InlineData("x")]
    public void Create_WithInvalidType_Throws(string type)
    {
        var act = () => PlatformTaxonomy.Create(type, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TaxonomyInvalidChange);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-08")]
    [Trait("AC", "AC-01")]
    public void ApplyChanges_AddsNodesBumpsVersionAndRaisesEventWithChangedCodes()
    {
        var taxonomy = Create();

        taxonomy.ApplyChanges(new[] { Add("it"), Add("dev", "it") }, Admin, At);

        taxonomy.TaxonomyVersion.Should().Be(2);
        taxonomy.Nodes.Should().HaveCount(2);
        var e = taxonomy.DomainEvents.Single().Should().BeOfType<PlatformTaxonomyUpdatedDomainEvent>().Which;
        (e.FromVersion, e.ToVersion).Should().Be((1, 2));
        e.ChangedCodes.Should().BeEquivalentTo("it", "dev");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-08")]
    [Trait("AC", "AC-02")]
    public void ApplyChanges_WithDuplicateCode_Throws()
    {
        var taxonomy = Create(Add("it"));

        Apply(taxonomy, Add("IT")).Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TaxonomyDuplicateCode);
        taxonomy.TaxonomyVersion.Should().Be(2);
    }

    [Fact]
    public void ApplyChanges_WithUnknownParent_Throws()
    {
        Apply(Create(), Add("dev", "nope")).Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TaxonomyParentNotFound);
    }

    [Fact]
    public void Edit_ReparentingUnderOwnDescendant_ThrowsCycle()
    {
        var taxonomy = Create(Add("a"), Add("b", "a"), Add("c", "b"));

        var reparent = new TaxonomyChange(TaxonomyChangeKind.Edit, "a", null, "c", null, null);

        Apply(taxonomy, reparent).Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TaxonomyCycle);
    }

    [Fact]
    public void Edit_OwnParent_ThrowsCycle()
    {
        var taxonomy = Create(Add("a"));

        Apply(taxonomy, new TaxonomyChange(TaxonomyChangeKind.Edit, "a", null, "a", null, null))
            .Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TaxonomyCycle);
    }

    [Fact]
    public void Add_DeeperThanSixLevels_Throws()
    {
        var chain = new List<TaxonomyChange> { Add("n1") };
        for (var i = 2; i <= 7; i++)
        {
            chain.Add(Add($"n{i}", $"n{i - 1}"));
        }

        Apply(Create(), chain.ToArray()).Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TaxonomyDepthExceeded);
    }

    [Fact]
    public void Add_ExactlySixLevels_IsAllowed()
    {
        var chain = new List<TaxonomyChange> { Add("n1") };
        for (var i = 2; i <= 6; i++)
        {
            chain.Add(Add($"n{i}", $"n{i - 1}"));
        }

        Apply(Create(), chain.ToArray()).Should().NotThrow();
    }

    [Fact]
    public void Remove_IsSoft_AndKeepsTheNodeResolvable()
    {
        var taxonomy = Create(Add("a"), Add("b", "a"));

        taxonomy.ApplyChanges(new[] { new TaxonomyChange(TaxonomyChangeKind.Remove, "a", null, null, null, null) }, Admin, At);

        taxonomy.Nodes.Should().HaveCount(2);
        taxonomy.Nodes.Single(n => n.Code == "a").IsActive.Should().BeFalse();
        taxonomy.Nodes.Single(n => n.Code == "b").IsActive.Should().BeTrue();
    }

    [Fact]
    public void Edit_UpdatesNameSynonymsAndReactivates()
    {
        var taxonomy = Create(Add("a"));
        taxonomy.ApplyChanges(new[] { new TaxonomyChange(TaxonomyChangeKind.Remove, "a", null, null, null, null) }, Admin, At);

        taxonomy.ApplyChanges(new[] { new TaxonomyChange(TaxonomyChangeKind.Edit, "a", new LocalizedText("x", "y"), null, new[] { "syn" }, true) }, Admin, At);

        var node = taxonomy.Nodes.Single();
        (node.Name.En, node.IsActive, node.Synonyms).Should().Be(("y", true, node.Synonyms));
        node.Synonyms.Should().Equal("syn");
    }

    [Fact]
    public void EditOrRemove_UnknownCode_ThrowsNodeNotFound()
    {
        Apply(Create(), new TaxonomyChange(TaxonomyChangeKind.Remove, "ghost", null, null, null, null))
            .Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.TaxonomyNodeNotFound);
    }

    [Fact]
    public void ApplyChanges_FailingHalfway_LeavesNodesUntouched()
    {
        var taxonomy = Create(Add("a"));

        Apply(taxonomy, Add("b"), Add("c", "missing")).Should().Throw<BusinessRuleViolationException>();

        taxonomy.Nodes.Should().ContainSingle();
        taxonomy.TaxonomyVersion.Should().Be(2);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-08")]
    [Trait("AC", "AC-03")]
    public void ApplyChanges_TwiceInARow_LaterWinsWithBothVersionsPublished()
    {
        var taxonomy = Create(Add("a"));

        taxonomy.ApplyChanges(new[] { new TaxonomyChange(TaxonomyChangeKind.Edit, "a", new LocalizedText("1", "1"), null, null, null) }, Admin, At);
        taxonomy.ApplyChanges(new[] { new TaxonomyChange(TaxonomyChangeKind.Edit, "a", new LocalizedText("2", "2"), null, null, null) }, Admin, At);

        taxonomy.Nodes.Single().Name.En.Should().Be("2");
        taxonomy.DomainEvents.OfType<PlatformTaxonomyUpdatedDomainEvent>().Select(e => e.ToVersion).Should().Equal(3, 4);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-08")]
    [Trait("AC", "AC-04")]
    public void ApplyChanges_ByNonAdministrator_ThrowsForbidden()
    {
        Action act = () => Create().ApplyChanges(new[] { Add("a") }, new Actor(Guid.NewGuid(), false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }

    [Fact]
    public void CreateSnapshot_CapturesTheCurrentVersionImmutably()
    {
        var taxonomy = Create(Add("a"));
        var snapshot = taxonomy.CreateSnapshot(At);

        taxonomy.ApplyChanges(new[] { Add("b") }, Admin, At);

        snapshot.TaxonomyVersion.Should().Be(2);
        snapshot.Nodes.Select(n => n.Code).Should().Equal("a");
    }

    [Theory]
    [InlineData("skills", true)]
    [InlineData("training-programs", true)]
    [InlineData("Skills", false)]
    [InlineData("a", false)]
    [InlineData(null, false)]
    public void TypeValidation(string? type, bool valid) => TaxonomyTypes.IsValid(type).Should().Be(valid);
}
