using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class PublishNewsArticleHandler : ICommandHandler<PublishNewsArticleCommand, Unit>
{
    private readonly INewsArticleRepository _articles;
    private readonly IContentCategorizationRepository _categorizations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PublishNewsArticleHandler(INewsArticleRepository articles, IContentCategorizationRepository categorizations, ICurrentUser user, TimeProvider clock)
    {
        _articles = articles;
        _categorizations = categorizations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(PublishNewsArticleCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        var categorization = await _categorizations.GetByArticleAsync(article.Id, ct);
        article.Publish(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime, categorization?.CategoryIds ?? Array.Empty<Guid>());
        return Result.Success();
    }
}
