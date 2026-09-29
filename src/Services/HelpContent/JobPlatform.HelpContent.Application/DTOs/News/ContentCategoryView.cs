using JobPlatform.HelpContent.Application.DTOs.Common;

namespace JobPlatform.HelpContent.Application.DTOs.News;

public sealed record ContentCategoryView(Guid CategoryId, LocalizedView Name, bool IsDeleted);
