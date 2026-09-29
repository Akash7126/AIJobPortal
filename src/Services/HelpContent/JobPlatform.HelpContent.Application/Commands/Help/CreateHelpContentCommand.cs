using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;

namespace JobPlatform.HelpContent.Application.Commands.Help;

public sealed record CreateHelpContentCommand(HelpKind Kind, string? TitleAr, string? TitleEn, string? BodyAr, string? BodyEn) : AdminCommand<HelpContentView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
