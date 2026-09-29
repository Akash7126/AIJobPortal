using FluentValidation.TestHelper;
using JobPlatform.HelpContent.Application.Commands.CompanyPage;
using JobPlatform.HelpContent.Application.Commands.Feedback;
using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.Queries.Help;
using JobPlatform.HelpContent.Application.Validators.CompanyPage;
using JobPlatform.HelpContent.Application.Validators.Feedback;
using JobPlatform.HelpContent.Application.Validators.Help;
using JobPlatform.HelpContent.Application.Validators.News;
using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.UnitTests;

public class ValidatorTests
{
    private readonly CreateNewsArticleValidator _createNews = new();
    private readonly AddNewsMediaValidator _newsMedia = new();
    private readonly UpdateContentCategorizationValidator _categorization = new();
    private readonly CreateHelpContentValidator _createHelp = new();
    private readonly UpdateHelpContentValidator _updateHelp = new();
    private readonly AttachHelpMediaValidator _helpMedia = new();
    private readonly SubmitHelpFeedbackValidator _feedback = new();
    private readonly SearchHelpContentValidator _search = new();
    private readonly EditCompanyPageValidator _companyPage = new();

    [Fact]
    public void CreateNewsArticle_Valid_HasNoErrors() =>
        _createNews.TestValidate(new CreateNewsArticleCommand(NewsKind.Article, null, "Title", null, "Body")).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CreateNewsArticle_MissingBothTitles_HasError() =>
        _createNews.TestValidate(new CreateNewsArticleCommand(NewsKind.Article, null, null, null, "Body")).Errors.Should().NotBeEmpty();

    [Fact]
    public void CreateNewsArticle_TitleTooLong_HasError() =>
        _createNews.TestValidate(new CreateNewsArticleCommand(NewsKind.Article, null, new string('a', 201), null, "Body"))
            .ShouldHaveValidationErrorFor(c => c.TitleEn);

    [Fact]
    public void CreateNewsArticle_BodyTooLong_HasError() =>
        _createNews.TestValidate(new CreateNewsArticleCommand(NewsKind.Article, null, "Title", null, new string('a', 50_001)))
            .ShouldHaveValidationErrorFor(c => c.BodyEn);

    [Fact]
    public void AddNewsMedia_Valid_HasNoErrors() =>
        _newsMedia.TestValidate(new AddNewsMediaCommand(Guid.NewGuid(), NewsMediaType.Image, "a.png", "image/png", 1024, new byte[1024], "alt"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void AddNewsMedia_ImageWithoutAltText_HasError() =>
        _newsMedia.TestValidate(new AddNewsMediaCommand(Guid.NewGuid(), NewsMediaType.Image, "a.png", "image/png", 1024, new byte[1024], null))
            .ShouldHaveValidationErrorFor(c => c.AltText);

    [Fact]
    public void AddNewsMedia_TooLarge_HasError() =>
        _newsMedia.TestValidate(new AddNewsMediaCommand(Guid.NewGuid(), NewsMediaType.Image, "a.png", "image/png", NewsArticle.MaxMediaSizeBytes + 1,
            Array.Empty<byte>(), "alt")).ShouldHaveValidationErrorFor(c => c.SizeBytes);

    [Fact]
    public void UpdateContentCategorization_TooManyTags_HasError() =>
        _categorization.TestValidate(new UpdateContentCategorizationCommand(Guid.NewGuid(), Array.Empty<Guid>(),
            Enumerable.Range(0, 21).Select(i => $"t{i}").ToArray())).ShouldHaveValidationErrorFor(c => c.Tags);

    [Fact]
    public void UpdateContentCategorization_ValidTags_HasNoErrors() =>
        _categorization.TestValidate(new UpdateContentCategorizationCommand(Guid.NewGuid(), Array.Empty<Guid>(), new[] { "urgent" }))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CreateHelpContent_Valid_HasNoErrors() =>
        _createHelp.TestValidate(new CreateHelpContentCommand(HelpKind.Faq, null, "Q", null, "A")).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void UpdateHelpContent_MissingBothBodies_HasError() =>
        _updateHelp.TestValidate(new UpdateHelpContentCommand(Guid.NewGuid(), null, "Q", null, null)).Errors.Should().NotBeEmpty();

    [Fact]
    public void AttachHelpMedia_WithoutCaptionsOrTextAlternative_HasError() =>
        _helpMedia.TestValidate(new AttachHelpMediaCommand(Guid.NewGuid(), HelpMediaType.Video, "v.mp4", "video/mp4", 1024, new byte[1024], null, null))
            .ShouldHaveValidationErrorFor(c => c.CaptionsRef);

    [Fact]
    public void AttachHelpMedia_WithTextAlternative_HasNoErrors() =>
        _helpMedia.TestValidate(new AttachHelpMediaCommand(Guid.NewGuid(), HelpMediaType.Video, "v.mp4", "video/mp4", 1024, new byte[1024], null, "Alt text"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void SubmitHelpFeedback_CommentTooLong_HasError() =>
        _feedback.TestValidate(new SubmitHelpFeedbackCommand(Guid.NewGuid(), FeedbackRating.Helpful, new string('a', 501)))
            .ShouldHaveValidationErrorFor(c => c.Comment);

    [Fact]
    public void SubmitHelpFeedback_Valid_HasNoErrors() =>
        _feedback.TestValidate(new SubmitHelpFeedbackCommand(Guid.NewGuid(), FeedbackRating.Helpful, "Great")).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void SearchHelpContent_EmptyKeyword_HasError() =>
        _search.TestValidate(new SearchHelpContentQuery("", null)).ShouldHaveValidationErrorFor(c => c.Keyword);

    [Fact]
    public void SearchHelpContent_PageSizeTooLarge_HasError() =>
        _search.TestValidate(new SearchHelpContentQuery("q", null, 1, 51)).ShouldHaveValidationErrorFor(c => c.PageSize);

    [Fact]
    public void EditCompanyPage_TooManyHighlights_HasError() =>
        _companyPage.TestValidate(new EditCompanyPageCommand(null, "bg", Enumerable.Range(0, 11).Select(i => $"h{i}").ToArray()))
            .ShouldHaveValidationErrorFor(c => c.Highlights);

    [Fact]
    public void EditCompanyPage_Valid_HasNoErrors() =>
        _companyPage.TestValidate(new EditCompanyPageCommand(null, "bg", new[] { "h1" })).ShouldNotHaveAnyValidationErrors();
}
