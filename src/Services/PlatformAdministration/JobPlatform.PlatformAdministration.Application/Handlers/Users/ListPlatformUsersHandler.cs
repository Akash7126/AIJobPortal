using JobPlatform.PlatformAdministration.Application.DTOs.Users;
using JobPlatform.PlatformAdministration.Application.Queries.Users;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Handlers.Users;

internal sealed class ListPlatformUsersHandler : IQueryHandler<ListPlatformUsersQuery, PagedResult<PlatformUserListItem>>
{
    private readonly IUserDirectory _directory;

    public ListPlatformUsersHandler(IUserDirectory directory) => _directory = directory;

    public Task<Result<PagedResult<PlatformUserListItem>>> Handle(ListPlatformUsersQuery request, CancellationToken ct) =>
        _directory.ListAsync(request.Type, request.Status, request.Search, new PageRequest(request.Page, request.PageSize), ct);
}
