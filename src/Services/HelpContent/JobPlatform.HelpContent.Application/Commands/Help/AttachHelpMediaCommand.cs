using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;

namespace JobPlatform.HelpContent.Application.Commands.Help;

public sealed record AttachHelpMediaCommand(Guid HelpContentId, HelpMediaType Type, string FileName, string ContentType, long SizeBytes, byte[] Content,
    string? CaptionsRef, string? TextAlternative) : AdminCommand<HelpMediaView>
{
    public override string ForbiddenErrorCode => ErrorCodes.HelpForbidden;
}
