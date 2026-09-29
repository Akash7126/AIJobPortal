using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class AddNewsMediaHandler : ICommandHandler<AddNewsMediaCommand, NewsMediaView>
{
    private readonly INewsArticleRepository _articles;
    private readonly IMediaStorage _storage;
    private readonly ICurrentUser _user;

    public AddNewsMediaHandler(INewsArticleRepository articles, IMediaStorage storage, ICurrentUser user)
    {
        _articles = articles;
        _storage = storage;
        _user = user;
    }

    public async Task<Result<NewsMediaView>> Handle(AddNewsMediaCommand request, CancellationToken ct)
    {
        var article = await _articles.GetByIdAsync(request.NewsArticleId, ct);
        if (article is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        await using var stream = new MemoryStream(request.Content);
        var storageKey = await _storage.SaveAsync(stream, request.FileName, request.ContentType, ct);
        var file = new NewsMediaFile(storageKey, request.SizeBytes, request.ContentType);
        var media = article.AddMedia(Guid.NewGuid(), request.Type, file, request.AltText, ActorFactory.From(_user));
        return new NewsMediaView(media.Id, media.Type.ToString(), _storage.UrlFor(storageKey), media.AltText);
    }
}
