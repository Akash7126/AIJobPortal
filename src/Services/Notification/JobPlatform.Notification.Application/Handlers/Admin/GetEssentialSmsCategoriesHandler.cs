using JobPlatform.Notification.Application.Queries.Admin;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Admin;

internal sealed class GetEssentialSmsCategoriesHandler : IQueryHandler<GetEssentialSmsCategoriesQuery, IReadOnlyCollection<string>>
{
    private readonly ISmsPolicyRepository _policy;

    public GetEssentialSmsCategoriesHandler(ISmsPolicyRepository policy) => _policy = policy;

    public async Task<Result<IReadOnlyCollection<string>>> Handle(GetEssentialSmsCategoriesQuery request, CancellationToken ct) =>
        Result.Success((await _policy.GetCurrentAsync(ct)).EssentialCategories);
}
