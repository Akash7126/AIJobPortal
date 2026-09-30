using JobPlatform.AccountIdentity.Domain.Rbac;

namespace JobPlatform.AccountIdentity.Application.Interfaces;

/// <summary>Role-to-permission map with immediate invalidation (Redis cache-aside, D-03: tokens carry role ids only).</summary>
public interface IRoleDirectory
{
    Task<IReadOnlyList<RolePermissions>> GetRolesForAccountAsync(Guid accountId, CancellationToken ct = default);
}
