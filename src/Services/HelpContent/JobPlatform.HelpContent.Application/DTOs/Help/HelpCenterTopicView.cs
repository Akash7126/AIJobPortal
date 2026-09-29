namespace JobPlatform.HelpContent.Application.DTOs.Help;

public sealed record HelpCenterTopicView(Guid? TopicId, string? TopicName, IReadOnlyList<HelpSearchResultView> Items);
