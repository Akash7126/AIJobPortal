using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

public enum FeedbackRating
{
    Helpful,
    NotHelpful
}

/// <summary>
/// AGG-34: a user's usefulness rating on a help article (handover section 3.5). INV-12: one feedback per (user, article) - a repeat
/// replaces the prior rather than creating a duplicate row (story AC-02); the repository upserts by looking the pair up first, mirroring
/// BC-05's media duplicate-detection pattern.
/// </summary>
public sealed class HelpFeedback : AggregateRoot<Guid>
{
    public const int MaxCommentLength = 500;

    private HelpFeedback()
    {
    }

    public Guid HelpContentId { get; private set; }

    public Guid UserId { get; private set; }

    public FeedbackRating Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public static HelpFeedback Submit(Guid id, Guid helpContentId, Guid userId, FeedbackRating rating, string? comment, DateTime nowUtc)
    {
        Check(CommentLengthRule(comment));
        return new HelpFeedback
        {
            Id = id, HelpContentId = helpContentId, UserId = userId, Rating = rating, Comment = comment, SubmittedAtUtc = nowUtc
        };
    }

    /// <summary>Replaces this user's prior rating/comment on the same article (the "latest replaces" rule, not a new aggregate).</summary>
    public void Replace(FeedbackRating rating, string? comment, DateTime nowUtc)
    {
        Check(CommentLengthRule(comment));
        Rating = rating;
        Comment = comment;
        SubmittedAtUtc = nowUtc;
    }

    private static IBusinessRule CommentLengthRule(string? comment) =>
        new BusinessRule("HC.Feedback.COMMENT_TOO_LONG", "The comment must be 500 characters or fewer.",
            comment is { Length: > MaxCommentLength });
}
