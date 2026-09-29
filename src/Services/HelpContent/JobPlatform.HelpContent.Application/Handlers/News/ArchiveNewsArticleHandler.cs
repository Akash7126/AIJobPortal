using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class ArchiveNewsArticleHandler : ICommandHandler<ArchiveNewsArticleCommand, Unit>
{
    private readonly INewsArticleRepository _articles;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ArchiveNewsArticleHandler(INewsArticleRepository articles, ICurrentUser user, TimeProvider clock)
    {
        _articles = articles;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ArchiveNewsArticleCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        article.Archive(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
