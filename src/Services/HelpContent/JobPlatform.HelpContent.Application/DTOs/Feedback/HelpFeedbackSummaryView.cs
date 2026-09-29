namespace JobPlatform.HelpContent.Application.DTOs.Feedback;

public sealed record HelpFeedbackSummaryView(Guid HelpContentId, int HelpfulCount, int NotHelpfulCount);
