using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.Help;

public sealed record HelpContentView(
    Guid HelpContentId, string Kind, LocalizedView Title, LocalizedView Body, int CurrentVersion, Guid? TopicId, IReadOnlyList<string> Roles,
    IReadOnlyList<HelpMediaView> Media, byte[] RowVersion);
