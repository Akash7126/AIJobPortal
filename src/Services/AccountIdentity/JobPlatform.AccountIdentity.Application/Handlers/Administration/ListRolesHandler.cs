using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.AccountIdentity.Application.Queries.Administration;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

internal sealed class ListRolesHandler : IQueryHandler<ListRolesQuery, IReadOnlyList<RoleView>>
{
    private readonly IIdentityReadStore _store;

    public ListRolesHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<RoleView>>> Handle(ListRolesQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListRolesAsync(ct));
}
