using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record UpdateContentCategorizationCommand(Guid NewsArticleId, IReadOnlyList<Guid> CategoryIds, IReadOnlyList<string> Tags) : AdminCommand<Unit>;
