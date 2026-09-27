using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain.UnitTests;

public class HelpFeedbackTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-01")]
    public void Submit_Valid_Succeeds()
    {
        var feedback = HelpFeedback.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FeedbackRating.Helpful, "Very useful", At);

        feedback.Rating.Should().Be(FeedbackRating.Helpful);
        feedback.Comment.Should().Be("Very useful");
    }

    [Fact]
    [Trait("Story", "US-3.7.2-06")]
    [Trait("AC", "AC-02")]
    public void Replace_ChangesRatingAndComment_SameAggregate()
    {
        var feedback = HelpFeedback.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FeedbackRating.Helpful, "Good", At);

        feedback.Replace(FeedbackRating.NotHelpful, "Changed my mind", At.AddDays(1));

        feedback.Rating.Should().Be(FeedbackRating.NotHelpful);
        feedback.Comment.Should().Be("Changed my mind");
        feedback.SubmittedAtUtc.Should().Be(At.AddDays(1));
    }

    [Fact]
    public void Submit_CommentTooLong_Throws()
    {
        var act = () => HelpFeedback.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FeedbackRating.Helpful,
            new string('a', HelpFeedback.MaxCommentLength + 1), At);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
