using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.Help;

public sealed record HelpSearchResultView(Guid HelpContentId, string Kind, LocalizedView Title, string? TopicName);
