using JobPlatform.HelpContent.Application.DTOs.Feedback;
using JobPlatform.HelpContent.Domain.Common;

namespace JobPlatform.HelpContent.Application.Queries.Feedback;

/// <summary>Story AC-03/AC-04: aggregate feedback is administrator-only.</summary>
public sealed record GetHelpFeedbackSummaryQuery(Guid HelpContentId) : AdminQuery<HelpFeedbackSummaryView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
