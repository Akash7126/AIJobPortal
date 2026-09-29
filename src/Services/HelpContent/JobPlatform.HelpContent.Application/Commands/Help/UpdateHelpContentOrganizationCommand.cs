using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.Help;

public sealed record UpdateHelpContentOrganizationCommand(Guid HelpContentId, Guid? TopicId, IReadOnlyList<HelpRole> Roles) : AdminCommand<Unit>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
