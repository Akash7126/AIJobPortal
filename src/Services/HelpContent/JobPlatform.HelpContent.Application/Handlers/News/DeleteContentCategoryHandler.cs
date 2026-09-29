using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class DeleteContentCategoryHandler : ICommandHandler<DeleteContentCategoryCommand, Unit>
{
    private readonly IContentCategoryRepository _categories;
    private readonly ICurrentUser _user;

    public DeleteContentCategoryHandler(IContentCategoryRepository categories, ICurrentUser user)
    {
        _categories = categories;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(DeleteContentCategoryCommand request, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(request.CategoryId, ct);
        if (category is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The category was not found.");
        }

        category.SoftDelete(ActorFactory.From(_user));
        return Result.Success();
    }
}
