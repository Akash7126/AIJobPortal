using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record PublishNewsArticleCommand(Guid NewsArticleId) : AdminCommand<Unit>;
