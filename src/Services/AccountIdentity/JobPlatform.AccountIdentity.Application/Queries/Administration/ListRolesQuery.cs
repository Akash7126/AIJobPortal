using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Queries.Administration;

public sealed record ListRolesQuery : AdminAuthorized, IQuery<IReadOnlyList<RoleView>>
{
    public ListRolesQuery() : base(Permissions.RolesRead)
    {
    }
}
