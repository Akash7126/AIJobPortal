using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain.Common;

namespace JobPlatform.HelpContent.Application.Commands.Help;

public sealed record CreateHelpTopicCommand(string? NameAr, string? NameEn) : AdminCommand<HelpTopicView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
