using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.Help;

public sealed record SetContextHelpMappingCommand(string PageKey, Guid HelpContentId) : AdminCommand<Unit>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
