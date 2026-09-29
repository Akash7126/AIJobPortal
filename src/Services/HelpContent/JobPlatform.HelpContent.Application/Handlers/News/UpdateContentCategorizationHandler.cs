using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class UpdateContentCategorizationHandler : ICommandHandler<UpdateContentCategorizationCommand, Unit>
{
    private readonly INewsArticleRepository _articles;
    private readonly IContentCategorizationRepository _categorizations;
    private readonly ICurrentUser _user;

    public UpdateContentCategorizationHandler(INewsArticleRepository articles, IContentCategorizationRepository categorizations, ICurrentUser user)
    {
        _articles = articles;
        _categorizations = categorizations;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(UpdateContentCategorizationCommand request, CancellationToken ct)
    {
        if (await _articles.GetByIdAsync(request.NewsArticleId, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The news article was not found.");
        }

        var categorization = await _categorizations.GetByArticleAsync(request.NewsArticleId, ct);
        if (categorization is null)
        {
            categorization = ContentCategorization.CreateFor(request.NewsArticleId);
            _categorizations.Add(categorization);
        }

        categorization.Assign(request.CategoryIds, request.Tags, ActorFactory.From(_user));
        return Result.Success();
    }
}
