using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class AccountRepository : IAccountRepository, IAccountUniquenessChecker
{
    private readonly IdentityDbContext _db;

    public AccountRepository(IdentityDbContext db) => _db = db;

    public Task<Account?> GetByIdAsync(AccountId id, CancellationToken ct = default) =>
        _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Account>> FindByLoginAsync(string login, ActorType? actorType, CancellationToken ct = default)
    {
        IQueryable<Account> query = _db.Accounts;
        if (login.Contains('@'))
        {
            if (!Email.TryCreate(login, out var email))
            {
                return Array.Empty<Account>();
            }

            query = query.Where(a => a.Email == email);
        }
        else
        {
            if (!MobileNumber.TryCreate(login, out var mobile))
            {
                return Array.Empty<Account>();
            }

            query = query.Where(a => a.Mobile == mobile);
        }

        if (actorType is { } type)
        {
            query = query.Where(a => a.ActorType == type);
        }

        return await query.OrderBy(a => a.CreatedAtUtc).ToListAsync(ct);
    }

    public void Add(Account account) => _db.Accounts.Add(account);

    public Task<bool> IsMobileTakenAsync(ActorType actorType, MobileNumber mobile, CancellationToken ct = default) =>
        _db.Accounts.AnyAsync(a => a.ActorType == actorType && a.Mobile == mobile, ct);

    public Task<bool> IsEmailTakenAsync(ActorType actorType, Email email, CancellationToken ct = default) =>
        _db.Accounts.AnyAsync(a => a.ActorType == actorType && a.Email == email, ct);

    public Task<bool> IsIdentityKeyTakenAsync(ActorType actorType, ExternalIdentityKey key, CancellationToken ct = default) =>
        _db.Accounts.AnyAsync(a => a.ActorType == actorType && a.IdentityKey == key, ct);
}
