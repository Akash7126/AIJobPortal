using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.AccountIdentity.Domain.Rbac;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository : IRoleRepository
{
    private readonly IdentityDbContext _db;

    public RoleRepository(IdentityDbContext db) => _db = db;

    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default) => _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken ct = default) => await _db.Roles.OrderBy(r => r.Name).ToListAsync(ct);

    public void Add(Role role) => _db.Roles.Add(role);
}
