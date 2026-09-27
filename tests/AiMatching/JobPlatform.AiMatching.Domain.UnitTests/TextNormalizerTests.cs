using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Python", "python")]
    [InlineData("  Python  ", "python")]
    [InlineData("C#", "c#")]
    public void Normalize_TrimsAndLowercases(string input, string expected) =>
        TextNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_FoldsArabicAlefVariantsAndRemovesDiacriticsAndTatweel()
    {
        // alef-with-hamza-above, alef-with-hamza-below, alef-madda -> bare alef; tashkeel + tatweel removed.
        var withHamza = TextNormalizer.Normalize("أحمد");
        var bareAlef = TextNormalizer.Normalize("احمد");

        withHamza.Should().Be(bareAlef);
        TextNormalizer.Normalize("مَرحبـــا").Should().Be(TextNormalizer.Normalize("مرحبا"));
    }

    [Fact]
    public void Normalize_FoldsAlefMaksuraAndTehMarbuta()
    {
        TextNormalizer.Normalize("مصطفى").Should().Be(TextNormalizer.Normalize("مصطفي"));
        TextNormalizer.Normalize("مكتبة").Should().Be(TextNormalizer.Normalize("مكتبه"));
    }

    [Fact]
    public void Normalize_CollapsesInternalWhitespaceAndSeparators()
    {
        TextNormalizer.Normalize("net_core-framework").Should().Be("net core framework");
        TextNormalizer.Normalize("a   b").Should().Be("a b");
    }

    [Fact]
    public void Normalize_NullOrWhitespace_ReturnsEmpty()
    {
        TextNormalizer.Normalize(null).Should().BeEmpty();
        TextNormalizer.Normalize("   ").Should().BeEmpty();
    }

    [Fact]
    public void SplitList_SplitsOnMultipleDelimitersAndTrimsAndDedupes()
    {
        var result = TextNormalizer.SplitList("C#, Java; SQL| C#\nPython");

        result.Should().Equal("C#", "Java", "SQL", "Python");
    }

    [Fact]
    public void SplitList_SplitsOnArabicCommaAndSemicolon()
    {
        var result = TextNormalizer.SplitList("جافا،بايثون؛ اس كيو ال");

        result.Should().HaveCount(3);
    }

    [Fact]
    public void SplitList_NullOrWhitespace_ReturnsEmpty() =>
        TextNormalizer.SplitList(null).Should().BeEmpty();

    [Theory]
    [InlineData("مرحبا بكم في المنصة", TextLanguage.Ar)]
    [InlineData("Welcome to the platform", TextLanguage.En)]
    [InlineData("12345 !!! ---", TextLanguage.Other)]
    [InlineData(null, TextLanguage.Other)]
    public void DetectLanguage_ReturnsDominantScript(string? text, TextLanguage expected) =>
        TextNormalizer.DetectLanguage(text).Should().Be(expected);

    [Fact]
    public void DetectLanguage_MixedButMostlyArabic_ReturnsAr() =>
        TextNormalizer.DetectLanguage("مرحبا مرحبا مرحبا hello").Should().Be(TextLanguage.Ar);
}
