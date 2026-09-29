using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain.Common;

namespace JobPlatform.HelpContent.Application.Queries.Help;

public sealed record ListHelpTopicsQuery : AdminQuery<IReadOnlyList<HelpTopicView>>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
