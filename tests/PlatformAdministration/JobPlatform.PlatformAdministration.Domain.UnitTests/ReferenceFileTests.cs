using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.UnitTests;

public class ReferenceFileTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);
    private static readonly string[] None = Array.Empty<string>();

    private static ReferenceChange Add(string code) => new(ReferenceChangeKind.Add, null, code, new LocalizedText(code + "-ar", code + "-en"), null);

    private static ReferenceFile FileWith(params string[] codes)
    {
        var file = ReferenceFile.Create(ReferenceFileType.Skills, At);
        file.ApplyChanges(codes.Select(Add).ToList(), false, None, Admin, At);
        file.ClearDomainEvents();
        return file;
    }

    [Fact]
    [Trait("Story", "US-3.1.4-07")]
    [Trait("AC", "AC-01")]
    public void ApplyChanges_AddsEntriesAsOneVersion()
    {
        var file = ReferenceFile.Create(ReferenceFileType.Skills, At);

        file.ApplyChanges(new[] { Add("a"), Add("b") }, false, None, Admin, At);

        file.Entries.Select(e => e.Code).Should().BeEquivalentTo("a", "b");
        file.Entries.Should().OnlyContain(e => e.IsActive);
        file.FileVersion.Should().Be(2);
        file.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReferenceFileUpdatedDomainEvent>().Which.FileVersion.Should().Be(2);
    }

    [Fact]
    public void ApplyChanges_WithDuplicateCode_ThrowsInvalidField()
    {
        var file = FileWith("a");

        var act = () => file.ApplyChanges(new[] { Add("A") }, false, None, Admin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ReferenceDuplicateCode);
        ex.ExternalCode.Should().Be("E-AUM-INVALID-FIELD");
    }

    [Fact]
    public void ApplyChanges_EditRenamesEntry()
    {
        var file = FileWith("a");
        var id = file.Entries.Single().Id;

        file.ApplyChanges(new[] { new ReferenceChange(ReferenceChangeKind.Edit, id, null, new LocalizedText("x", "y"), null) }, false, None, Admin, At);

        file.Entries.Single().Name.En.Should().Be("y");
    }

    [Fact]
    public void ApplyChanges_EditOrRemoveUnknownEntry_ThrowsEntryNotFound()
    {
        var file = FileWith("a");

        var act = () => file.ApplyChanges(new[] { new ReferenceChange(ReferenceChangeKind.Remove, Guid.NewGuid(), null, null, null) }, false, None, Admin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.ReferenceEntryNotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-07")]
    [Trait("AC", "AC-02")]
    public void Remove_NotInUse_SoftRemoves()
    {
        var file = FileWith("a");
        var id = file.Entries.Single().Id;

        file.ApplyChanges(new[] { new ReferenceChange(ReferenceChangeKind.Remove, id, null, null, null) }, false, None, Admin, At);

        file.Entries.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-07")]
    [Trait("AC", "AC-02")]
    public void Remove_InUseWithoutConfirmation_ThrowsEntryInUse()
    {
        var file = FileWith("a");
        var id = file.Entries.Single().Id;

        var act = () => file.ApplyChanges(new[] { new ReferenceChange(ReferenceChangeKind.Remove, id, null, null, null) }, false, new[] { "A" }, Admin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.ReferenceEntryInUse);
        ex.ExternalCode.Should().Be("E-AUM-ENTRY-IN-USE");
        ex.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    public void Remove_InUseWithConfirmation_Succeeds()
    {
        var file = FileWith("a");
        var id = file.Entries.Single().Id;

        file.ApplyChanges(new[] { new ReferenceChange(ReferenceChangeKind.Remove, id, null, null, null) }, true, new[] { "a" }, Admin, At);

        file.Entries.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_InUseWithoutConfirmation_ThrowsEntryInUse()
    {
        var file = FileWith("a");
        var id = file.Entries.Single().Id;

        var act = () => file.ApplyChanges(new[] { new ReferenceChange(ReferenceChangeKind.Edit, id, null, null, false) }, false, new[] { "a" }, Admin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.ReferenceEntryInUse);
    }

    [Fact]
    public void RemovalCandidateCodes_ListsRemovedAndDeactivatedActiveEntries()
    {
        var file = FileWith("a", "b", "c");
        var ids = file.Entries.ToDictionary(e => e.Code, e => e.Id);
        var changes = new[]
        {
            new ReferenceChange(ReferenceChangeKind.Remove, ids["a"], null, null, null),
            new ReferenceChange(ReferenceChangeKind.Edit, ids["b"], null, null, false),
            new ReferenceChange(ReferenceChangeKind.Edit, ids["c"], null, new LocalizedText("x", "y"), null)
        };

        file.RemovalCandidateCodes(changes).Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-07")]
    [Trait("AC", "AC-03")]
    public void ApplyChanges_TwiceInARow_EachSaveBumpsVersion()
    {
        var file = FileWith("a");

        file.ApplyChanges(new[] { Add("b") }, false, None, Admin, At);
        file.ApplyChanges(new[] { Add("c") }, false, None, new Actor(Guid.NewGuid(), true), At);

        file.FileVersion.Should().Be(4);
        file.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-07")]
    [Trait("AC", "AC-04")]
    public void ApplyChanges_ByNonAdministrator_ThrowsForbidden()
    {
        var file = FileWith("a");

        var act = () => file.ApplyChanges(new[] { Add("b") }, false, None, new Actor(Guid.NewGuid(), false), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }

    [Fact]
    public void ApplyChanges_WithNoChanges_ThrowsInvalidChange()
    {
        var act = () => FileWith("a").ApplyChanges(Array.Empty<ReferenceChange>(), false, None, Admin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.ReferenceInvalidChange);
    }
}
