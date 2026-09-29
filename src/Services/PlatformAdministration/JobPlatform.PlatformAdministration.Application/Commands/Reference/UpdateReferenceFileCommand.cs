using JobPlatform.PlatformAdministration.Application.DTOs.Common;
using JobPlatform.PlatformAdministration.Application.DTOs.Reference;

namespace JobPlatform.PlatformAdministration.Application.Commands.Reference;

/// <summary>US-3.1.4-07: apply entry changes to a reference file as one saved version. <paramref name="ConfirmInUse"/> allows removing referenced entries.</summary>
public sealed record UpdateReferenceFileCommand(string Type, bool ConfirmInUse, IReadOnlyList<ReferenceChangeRequest>? Changes)
    : AdminCommand<VersionResult>, ILaterSaveWinsCommand;
