using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Commands.News;

public sealed record DeleteContentCategoryCommand(Guid CategoryId) : AdminCommand<Unit>;
