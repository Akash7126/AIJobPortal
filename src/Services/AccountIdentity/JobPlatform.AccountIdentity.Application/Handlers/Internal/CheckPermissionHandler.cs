using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Queries.Internal;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Internal;

internal sealed class CheckPermissionHandler : IQueryHandler<CheckPermissionQuery, PermissionCheckResultDto>
{
    private readonly IRoleDirectory _roles;

    public CheckPermissionHandler(IRoleDirectory roles) => _roles = roles;

    public async Task<Result<PermissionCheckResultDto>> Handle(CheckPermissionQuery request, CancellationToken ct)
    {
        var roles = await _roles.GetRolesForAccountAsync(request.AccountId, ct);
        var decision = AccessPolicy.Authorise(roles, request.Permission);
        return new PermissionCheckResultDto(request.AccountId, request.Permission, decision.Allowed);
    }
}
