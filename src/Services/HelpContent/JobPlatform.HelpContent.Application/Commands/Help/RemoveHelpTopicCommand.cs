using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.Help;

public sealed record RemoveHelpTopicCommand(Guid TopicId) : AdminCommand<Unit>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
