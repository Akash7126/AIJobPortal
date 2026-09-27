using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.UnitTests;

public class ContentCategorizationTests
{
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    [Trait("AC", "AC-01")]
    public void Assign_SetsCategoriesAndTags()
    {
        var articleId = Guid.NewGuid();
        var categorization = ContentCategorization.CreateFor(articleId);
        var categoryId = Guid.NewGuid();

        categorization.Assign(new[] { categoryId }, new[] { "urgent" }, Admin);

        categorization.CategoryIds.Should().ContainSingle().Which.Should().Be(categoryId);
        categorization.Tags.Should().ContainSingle().Which.Should().Be("urgent");
        categorization.HasAnyCategoryOrTag.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    [Trait("AC", "AC-03")]
    public void Assign_Repeatedly_LaterSaveWins_NoConcurrencyRejection()
    {
        var categorization = ContentCategorization.CreateFor(Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        categorization.Assign(new[] { first }, Array.Empty<string>(), Admin);
        categorization.Assign(new[] { second }, Array.Empty<string>(), Admin);

        categorization.CategoryIds.Should().ContainSingle().Which.Should().Be(second);
    }

    [Fact]
    public void Assign_ByNonAdministrator_ThrowsForbidden()
    {
        var categorization = ContentCategorization.CreateFor(Guid.NewGuid());

        var act = () => categorization.Assign(Array.Empty<Guid>(), Array.Empty<string>(), new Actor(Guid.NewGuid(), false));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.CategorizationAdminOnly);
        ex.ExternalCode.Should().Be("E-NEWSU-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.7.1-04")]
    [Trait("AC", "AC-02")]
    public void ContentCategory_SoftDelete_MarksDeleted_NotRemoved()
    {
        var category = ContentCategory.Create(Guid.NewGuid(), new LocalizedText(null, "Announcements"), Admin);

        category.SoftDelete(Admin);

        category.IsDeleted.Should().BeTrue();
    }
}
