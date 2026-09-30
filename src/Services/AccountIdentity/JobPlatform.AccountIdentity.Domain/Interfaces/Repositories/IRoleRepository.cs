using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;

namespace JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default);

    Task<IReadOnlyList<Role>> ListAsync(CancellationToken ct = default);

    void Add(Role role);
}
