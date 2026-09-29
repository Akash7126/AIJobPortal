namespace JobPlatform.HelpContent.Application.DTOs.Help;

public sealed record HelpMediaView(Guid MediaId, string Type, string Url, string? CaptionsRef, string? TextAlternative);
