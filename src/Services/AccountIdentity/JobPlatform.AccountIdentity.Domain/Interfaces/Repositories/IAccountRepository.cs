using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(AccountId id, CancellationToken ct = default);

    /// <summary>Finds candidates by e-mail or mobile. One person may hold accounts of several actor types with the same mobile.</summary>
    Task<IReadOnlyList<Account>> FindByLoginAsync(string login, ActorType? actorType, CancellationToken ct = default);

    void Add(Account account);
}
