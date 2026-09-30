using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class EditNewsArticleHandler : ICommandHandler<EditNewsArticleCommand, NewsArticleView>
{
    private readonly INewsArticleRepository _articles;
    private readonly ICurrentUser _user;

    public EditNewsArticleHandler(INewsArticleRepository articles, ICurrentUser user)
    {
        _articles = articles;
        _user = user;
    }

    public async Task<Result<NewsArticleView>> Handle(EditNewsArticleCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        article.EditDraft(new LocalizedText(request.TitleAr, request.TitleEn), new LocalizedText(request.BodyAr, request.BodyEn), ActorFactory.From(_user));
        return CreateNewsArticleHandler.ToView(article);
    }
}
