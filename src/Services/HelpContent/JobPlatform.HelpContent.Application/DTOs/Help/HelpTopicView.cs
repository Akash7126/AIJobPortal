using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.Help;

public sealed record HelpTopicView(Guid TopicId, LocalizedView Name, bool IsRemoved);
