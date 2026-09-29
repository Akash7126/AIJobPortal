using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.DTOs.Common;
using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class CreateContentCategoryHandler : ICommandHandler<CreateContentCategoryCommand, ContentCategoryView>
{
    private readonly IContentCategoryRepository _categories;
    private readonly ICurrentUser _user;

    public CreateContentCategoryHandler(IContentCategoryRepository categories, ICurrentUser user)
    {
        _categories = categories;
        _user = user;
    }

    public async Task<Result<ContentCategoryView>> Handle(CreateContentCategoryCommand request, CancellationToken ct)
    {
        var category = ContentCategory.Create(Guid.NewGuid(), new LocalizedText(request.NameAr, request.NameEn), ActorFactory.From(_user));
        _categories.Add(category);
        return await Task.FromResult(new ContentCategoryView(category.Id, new LocalizedView(category.Name.Ar, category.Name.En), category.IsDeleted));
    }
}
