using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class ResumeParsedDataTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static ParserResult Result(TextLanguage language = TextLanguage.En, decimal confidence = 90) =>
        new(language, new[] { new ParsedFieldInput(ParsedFieldName.Skills, "C#, SQL", confidence) }, new[] { "C#", "SQL" }, new[] { "Developer" }, 3, "model-1");

    [Fact]
    public void FromParserResult_SupportedLanguage_RaisesComputedEventAndKeepsConfidence()
    {
        var data = ResumeParsedData.FromParserResult(Guid.NewGuid(), Guid.NewGuid(), "sha", Result(confidence: 90), lowConfidenceThresholdPercent: 70, At);

        data.Status.Should().Be(ParseStatus.Parsed);
        data.LanguageFlagged.Should().BeFalse();
        data.Fields.Single().Confidence.Should().Be(90);
        data.Fields.Single().NeedsReview.Should().BeFalse();
        data.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ResumeParsedDataComputedDomainEvent>();
    }

    [Theory]
    [InlineData(69, true)]
    [InlineData(70, false)]
    [InlineData(71, false)]
    public void FromParserResult_ConfidenceBoundary_FlagsNeedsReviewBelowThreshold(decimal confidence, bool expectedNeedsReview)
    {
        var data = ResumeParsedData.FromParserResult(Guid.NewGuid(), Guid.NewGuid(), "sha", Result(confidence: confidence), lowConfidenceThresholdPercent: 70, At);

        data.Fields.Single().NeedsReview.Should().Be(expectedNeedsReview);
    }

    [Fact]
    public void FromParserResult_UnsupportedLanguage_ReducesConfidenceAndAlwaysFlags()
    {
        var data = ResumeParsedData.FromParserResult(Guid.NewGuid(), Guid.NewGuid(), "sha", Result(TextLanguage.Other, confidence: 100),
            lowConfidenceThresholdPercent: 70, At);

        data.LanguageFlagged.Should().BeTrue();
        data.Fields.Single().Confidence.Should().Be(100 * ResumeParsedData.UnsupportedLanguageFactor);
        data.Fields.Single().NeedsReview.Should().BeTrue();
    }

    [Fact]
    public void Unreadable_CreatesFailedRecordWithFailureCode()
    {
        var data = ResumeParsedData.Unreadable(Guid.NewGuid(), Guid.NewGuid(), "sha", "model-1", At);

        data.Status.Should().Be(ParseStatus.Failed);
        data.FailureCode.Should().Be(AiErrorCodes.UnsupportedFormat);
        data.Fields.Should().BeEmpty();
        data.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ResumeParsedDataComputedDomainEvent>()
            .Which.Status.Should().Be(ParseStatus.Failed);
    }

    [Fact]
    public void Supersede_SetsSupersededBy()
    {
        var data = ResumeParsedData.FromParserResult(Guid.NewGuid(), Guid.NewGuid(), "sha", Result(), 70, At);
        var newId = Guid.NewGuid();

        data.Supersede(newId);

        data.SupersededBy.Should().Be(newId);
    }

    [Fact]
    public void Supersede_WithOwnId_Throws()
    {
        var data = ResumeParsedData.FromParserResult(Guid.NewGuid(), Guid.NewGuid(), "sha", Result(), 70, At);

        var act = () => data.Supersede(data.Id);

        act.Should().Throw<JobPlatform.SharedKernel.Domain.BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.InvalidInput);
    }

    [Fact]
    public void FieldsNeedingReview_ReturnsOnlyFlaggedFields()
    {
        var parserResult = new ParserResult(TextLanguage.En,
            new[]
            {
                new ParsedFieldInput(ParsedFieldName.Skills, "C#", 90),
                new ParsedFieldInput(ParsedFieldName.EducationHistory, "BSc", 50)
            }, new[] { "C#" }, Array.Empty<string>(), 3, "model-1");
        var data = ResumeParsedData.FromParserResult(Guid.NewGuid(), Guid.NewGuid(), "sha", parserResult, 70, At);

        data.FieldsNeedingReview.Should().ContainSingle().Which.Name.Should().Be(ParsedFieldName.EducationHistory);
    }
}
