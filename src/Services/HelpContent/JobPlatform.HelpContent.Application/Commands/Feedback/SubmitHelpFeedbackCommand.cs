using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.Feedback;

public sealed record SubmitHelpFeedbackCommand(Guid HelpContentId, FeedbackRating Rating, string? Comment) : AuthenticatedCommand<Unit>;
