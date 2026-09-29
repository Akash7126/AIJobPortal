using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record ArchiveNewsArticleCommand(Guid NewsArticleId) : AdminCommand<Unit>;
