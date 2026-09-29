using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.DTOs.Common;
using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class CreateNewsArticleHandler : ICommandHandler<CreateNewsArticleCommand, NewsArticleMutationResult>
{
    private readonly INewsArticleRepository _articles;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public CreateNewsArticleHandler(INewsArticleRepository articles, ICurrentUser user, TimeProvider clock)
    {
        _articles = articles;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<NewsArticleMutationResult>> Handle(CreateNewsArticleCommand request, CancellationToken ct)
    {
        var title = new LocalizedText(request.TitleAr, request.TitleEn);
        var body = new LocalizedText(request.BodyAr, request.BodyEn);
        var hash = NewsArticle.ComputeContentHash(title, body);

        // INV-02: an identical draft resubmitted updates the existing draft instead of creating a duplicate.
        var existing = await _articles.GetDraftByContentHashAsync(hash, ct);
        if (existing is not null)
        {
            existing.EditDraft(title, body, ActorFactory.From(_user));
            return new NewsArticleMutationResult(ToView(existing), true);
        }

        var article = NewsArticle.CreateDraft(Guid.NewGuid(), request.Kind, title, body, ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        _articles.Add(article);
        return new NewsArticleMutationResult(ToView(article), false);
    }

    internal static NewsArticleView ToView(NewsArticle a) => new(
        a.Id, a.Kind.ToString(), new LocalizedView(a.Title.Ar, a.Title.En), new LocalizedView(a.Body.Ar, a.Body.En), a.Status.ToString(),
        a.Media.Select(m => new NewsMediaView(m.Id, m.Type.ToString(), m.File.StorageKey, m.AltText)).ToArray(),
        Array.Empty<Guid>(), Array.Empty<string>(), a.CreatedBy, a.CreatedAtUtc, a.PublishedAtUtc, a.ArchivedAtUtc, a.RowVersion);
}
