using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Infrastructure;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobPlatform.AccountIdentity.Infrastructure.IntegrationTests;

public class PersistenceTests : IAsyncLifetime
{
    private static readonly PasswordHash Hash = new("h:pw");
    private IdentityTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new IdentityTestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private Account NewAccount(ActorType type = ActorType.JobSeeker, string mobile = "+970591111111", string? email = "a@example.com", string? identity = null) =>
        Account.Register(new RegistrationDetails(type, "Test", email is null ? null : Email.Create(email), MobileNumber.Create(mobile),
            identity is null ? null : ExternalIdentityKey.Create(identity)), Hash, _db.Clock);

    private async Task SaveAsync(params Account[] accounts)
    {
        await using var context = _db.NewContext();
        context.Accounts.AddRange(accounts);
        await context.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ INV-01 unique indexes (final guard behind the uniqueness checker)

    [Fact]
    public async Task UniqueIndex_ActorTypeAndMobile_RejectsADuplicate_ButAllowsTheSameMobileForAnotherActorType()
    {
        await SaveAsync(NewAccount(mobile: "+970591111111", email: "one@example.com"));

        await SaveAsync(NewAccount(ActorType.Employer, "+970591111111", "two@example.com", "C-1"));
        var act = () => SaveAsync(NewAccount(mobile: "+970591111111", email: "three@example.com"));

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task UniqueIndex_ActorTypeAndEmail_IsFilteredToNonNullEmails()
    {
        await SaveAsync(NewAccount(mobile: "+970591111111", email: "same@example.com"));

        await SaveAsync(NewAccount(mobile: "+970592222222", email: null), NewAccount(mobile: "+970593333333", email: null));
        var duplicate = () => SaveAsync(NewAccount(mobile: "+970594444444", email: "same@example.com"));

        await duplicate.Should().ThrowAsync<UniqueConstraintViolationException>();
        (await _db.NewContext().Accounts.CountAsync()).Should().Be(3, "several accounts may have no e-mail");
    }

    [Fact]
    public async Task UniqueIndex_ActorTypeAndExternalIdentityKey_RejectsDuplicateCompanyIds()
    {
        await SaveAsync(NewAccount(ActorType.Employer, "+970591111111", "e1@example.com", "COMPANY-1"));

        var duplicate = () => SaveAsync(NewAccount(ActorType.Employer, "+970592222222", "e2@example.com", "COMPANY-1"));
        await duplicate.Should().ThrowAsync<UniqueConstraintViolationException>();
        await SaveAsync(NewAccount(ActorType.ExternalJobSite, "+970593333333", "p@example.com", "COMPANY-1"));
    }

    [Fact]
    public async Task UniqueIndex_OneActiveCredentialPerPartner_AllowsRevokedHistory()
    {
        var partner = NewAccount(ActorType.ExternalJobSite, identity: "p");
        partner.ApproveByStaff(Actor.AuthorisedStaff(Guid.NewGuid()), _db.Clock);
        await SaveAsync(partner);

        await using (var context = _db.NewContext())
        {
            var first = ApiCredential.Issue(partner, "key-1", "h1", new CredentialControls(), null, partner.Id.Value, _db.Clock);
            first.Revoke(partner.Id.Value, _db.Clock);
            context.ApiCredentials.Add(first);
            context.ApiCredentials.Add(ApiCredential.Issue(partner, "key-2", "h2", new CredentialControls(), first, partner.Id.Value, _db.Clock));
            await context.SaveChangesAsync();
        }

        await using var again = _db.NewContext();
        again.ApiCredentials.Add(ApiCredential.Issue(partner, "key-3", "h3", new CredentialControls(), null, partner.Id.Value, _db.Clock));
        var act = () => again.SaveChangesAsync();
        await act.Should().ThrowAsync<UniqueConstraintViolationException>("INV-13: one Active credential per partner");
    }

    [Fact]
    public async Task UniqueIndex_KeyId_IsUnique()
    {
        var partner = NewAccount(ActorType.ExternalJobSite, identity: "p");
        partner.ApproveByStaff(Actor.AuthorisedStaff(Guid.NewGuid()), _db.Clock);
        var other = NewAccount(ActorType.ExternalJobSite, "+970592222222", "o@example.com", "q");
        other.ApproveByStaff(Actor.AuthorisedStaff(Guid.NewGuid()), _db.Clock);
        await SaveAsync(partner, other);
        await using (var context = _db.NewContext())
        {
            context.ApiCredentials.Add(ApiCredential.Issue(partner, "same-key", "h", new CredentialControls(), null, partner.Id.Value, _db.Clock));
            await context.SaveChangesAsync();
        }

        await using var duplicate = _db.NewContext();
        duplicate.ApiCredentials.Add(ApiCredential.Issue(other, "same-key", "h", new CredentialControls(), null, other.Id.Value, _db.Clock));
        var act = () => duplicate.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task UniqueIndex_GuestAndPolicyVersion_PreventsDuplicateConsentRows()
    {
        var guest = Guid.NewGuid();
        await using (var context = _db.NewContext())
        {
            context.PrivacyConsents.Add(PrivacyConsent.Create(guest, "v1", ConsentChoices.NecessaryOnly, Language.En, _db.Clock));
            context.PrivacyConsents.Add(PrivacyConsent.Create(guest, "v2", ConsentChoices.NecessaryOnly, Language.En, _db.Clock));
            await context.SaveChangesAsync();
        }

        await using var duplicate = _db.NewContext();
        duplicate.PrivacyConsents.Add(PrivacyConsent.Create(guest, "v1", new ConsentChoices(true, true, true), Language.Ar, _db.Clock));
        var act = () => duplicate.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    // ------------------------------------------------------------------ mapping, converters, concurrency

    [Fact]
    public async Task AccountAggregate_RoundTripsThroughPrivateSettersBackingFieldsAndValueConverters()
    {
        var account = NewAccount(ActorType.Employer, "+970 59-111-1111", "MiXeD@Example.COM", " company-9 ");
        account.IssueActivationChallenge("h:otp", _db.Clock);
        account.IssueEmailVerification("h:tok", _db.Clock);
        await SaveAsync(account);

        await using var context = _db.NewContext();
        var loaded = await context.Accounts.SingleAsync(a => a.Id == account.Id);

        loaded.Email!.Value.Should().Be("mixed@example.com");
        loaded.Mobile.Value.Should().Be("+970591111111");
        loaded.IdentityKey!.Value.Should().Be("COMPANY-9");
        loaded.Standing.Should().Be(AccountStanding.Pending);
        loaded.ActorType.Should().Be(ActorType.Employer);
        loaded.ActivationChallenge!.CodeHash.Should().Be("h:otp");
        loaded.ActivationChallenge.ExpiresAtUtc.Should().Be(_db.Clock.GetUtcNow().UtcDateTime.AddMinutes(10));
        loaded.ActivationChallenge.ExpiresAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        loaded.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        loaded.StatusHistory.Should().ContainSingle(h => h.To == AccountStanding.Pending);
        loaded.RoleAssignments.Should().ContainSingle(r => r.RoleId == WellKnownRoles.Employer);
        loaded.PasswordHash.Value.Should().Be("h:pw");
        loaded.EmailVerificationTokenHash.Should().Be("h:tok");
        loaded.Version.Should().Be(1);
    }

    [Fact]
    public async Task ChildCollectionChanges_ArePersisted_HistoryRolesAndTheClearedChallenge()
    {
        var account = NewAccount();
        account.IssueActivationChallenge("h:123456", _db.Clock);
        await SaveAsync(account);

        await using (var context = _db.NewContext())
        {
            var loaded = await context.Accounts.SingleAsync(a => a.Id == account.Id);
            loaded.Activate("123456", new PlainVerifier(), _db.Clock);
            loaded.AssignRole(RoleId.New(), _db.Clock);
            await context.SaveChangesAsync();
        }

        await using var check = _db.NewContext();
        var reloaded = await check.Accounts.SingleAsync(a => a.Id == account.Id);
        reloaded.Standing.Should().Be(AccountStanding.Active);
        reloaded.ActivationChallenge.Should().BeNull();
        reloaded.StatusHistory.Should().HaveCount(2);
        reloaded.RoleAssignments.Should().HaveCount(2);
    }

    private sealed class PlainVerifier : ISecretVerifier
    {
        public bool Verify(string secret, string hash) => hash == "h:" + secret;
    }

    [Fact]
    public async Task ConcurrentUpdates_OfTheSameAccount_AreDetectedByTheRowVersion()
    {
        var account = NewAccount();
        await SaveAsync(account);
        await using var first = _db.NewContext();
        await using var second = _db.NewContext();
        var a = await first.Accounts.SingleAsync(x => x.Id == account.Id);
        var b = await second.Accounts.SingleAsync(x => x.Id == account.Id);

        a.ResetCredentials(Actor.Administrator(Guid.NewGuid()), _db.Clock);
        await first.SaveChangesAsync();
        b.ResetCredentials(Actor.Administrator(Guid.NewGuid()), _db.Clock);
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    // ------------------------------------------------------------------ outbox written with the aggregate

    [Fact]
    public async Task Registering_WritesTheAccountCreatedOutboxRow_InTheSameSave()
    {
        var account = NewAccount();
        _db.Correlation.Set(Guid.NewGuid());
        await SaveAsync(account);

        await using var context = _db.NewContext();
        var row = await context.Set<OutboxMessage>().SingleAsync();
        row.Type.Should().Be("AccountCreated");
        row.AggregateId.Should().Be(account.Id.ToString());
        row.Payload.Should().Contain("\"actorType\":\"JobSeeker\"").And.NotContain("+970591111111").And.NotContain("a@example.com");
        row.Status.Should().Be(OutboxStatus.Pending);
        account.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ADuplicate_RollsBackTheOutboxRowTogetherWithTheAggregate()
    {
        await SaveAsync(NewAccount());
        var duplicate = () => SaveAsync(NewAccount(email: "other@example.com"));

        await duplicate.Should().ThrowAsync<UniqueConstraintViolationException>();

        await using var context = _db.NewContext();
        (await context.Set<OutboxMessage>().CountAsync()).Should().Be(1, "only the first registration's event exists");
        (await context.Accounts.CountAsync()).Should().Be(1);
    }

    // ------------------------------------------------------------------ repositories + read store

    [Fact]
    public async Task Repository_FindByLogin_ByEmailOrMobile_OptionallyFilteredByActorType()
    {
        await SaveAsync(NewAccount(mobile: "+970591111111", email: "shared@example.com"),
            NewAccount(ActorType.Employer, "+970591111111", "emp@example.com", "C-5"));
        await using var context = _db.NewContext();
        var repository = new AccountRepository(context);

        (await repository.FindByLoginAsync("+970591111111", null)).Should().HaveCount(2);
        (await repository.FindByLoginAsync("+970 591-111-111", ActorType.Employer)).Should().ContainSingle().Which.ActorType.Should().Be(ActorType.Employer);
        (await repository.FindByLoginAsync("SHARED@example.com", null)).Should().ContainSingle();
        (await repository.FindByLoginAsync("nobody@example.com", null)).Should().BeEmpty();
        (await repository.FindByLoginAsync("not-a-phone", null)).Should().BeEmpty();
        (await repository.FindByLoginAsync("bad@@", null)).Should().BeEmpty();
    }

    [Fact]
    public async Task UniquenessChecker_ReflectsWhatIsStored()
    {
        await SaveAsync(NewAccount(ActorType.Employer, "+970591111111", "e@example.com", "C-7"));
        await using var context = _db.NewContext();
        var checker = new AccountRepository(context);

        (await checker.IsMobileTakenAsync(ActorType.Employer, MobileNumber.Create("+970591111111"))).Should().BeTrue();
        (await checker.IsMobileTakenAsync(ActorType.JobSeeker, MobileNumber.Create("+970591111111"))).Should().BeFalse();
        (await checker.IsEmailTakenAsync(ActorType.Employer, Email.Create("E@example.com"))).Should().BeTrue();
        (await checker.IsIdentityKeyTakenAsync(ActorType.Employer, ExternalIdentityKey.Create("c-7"))).Should().BeTrue();
        (await checker.IsIdentityKeyTakenAsync(ActorType.Employer, ExternalIdentityKey.Create("c-8"))).Should().BeFalse();
    }

    [Fact]
    public async Task ReadStore_ProjectsStandingHistoryPagingAndFilters()
    {
        var one = NewAccount(mobile: "+970591111111", email: "one@example.com");
        var two = NewAccount(mobile: "+970592222222", email: "two@example.com");
        _db.Clock.Advance(TimeSpan.FromMinutes(1));
        two.Ban(Actor.Administrator(Guid.NewGuid()), "fraud", _db.Clock);
        await SaveAsync(one, two);
        await using var context = _db.NewContext();
        var store = new IdentityReadStore(context, InMemoryCache(_db.Clock), NullLogger<IdentityReadStore>.Instance);

        var standing = await store.GetAccountStandingAsync(two.Id.Value);
        standing!.Standing.Should().Be("Banned");
        standing.History.Should().HaveCount(2);
        standing.History.Last().Reason.Should().Be("fraud");
        (await store.GetAccountSummaryAsync(one.Id.Value))!.ActorType.Should().Be(ActorType.JobSeeker);
        (await store.GetAccountSummaryAsync(Guid.NewGuid())).Should().BeNull();

        var banned = await store.ListAccountsAsync(new AccountListFilter(null, "Banned", null), new PageRequest(1, 10));
        banned.TotalCount.Should().Be(1);
        var byEmail = await store.ListAccountsAsync(new AccountListFilter(null, null, "one@example.com"), new PageRequest(1, 10));
        byEmail.Items.Should().ContainSingle().Which.AccountId.Should().Be(one.Id.Value);
        var byMobile = await store.ListAccountsAsync(new AccountListFilter(null, null, "+970592222222"), new PageRequest(1, 10));
        byMobile.Items.Should().ContainSingle().Which.AccountId.Should().Be(two.Id.Value);
        var paged = await store.ListAccountsAsync(new AccountListFilter(null, null, null), new PageRequest(2, 1));
        paged.Items.Should().HaveCount(1);
        paged.TotalCount.Should().Be(2);
        paged.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task ReadStore_AccessLog_PagesAndFilters()
    {
        var account = Guid.NewGuid();
        await using (var context = _db.NewContext())
        {
            for (var i = 0; i < 5; i++)
            {
                context.AccessLog.Add(new AccessLogRecord
                {
                    Id = Guid.NewGuid(), AtUtc = _db.Clock.GetUtcNow().UtcDateTime.AddMinutes(i), AccountId = i < 3 ? account : null,
                    Action = "auth.login", Resource = "auth", Decision = i % 2 == 0 ? "Allow" : "Deny"
                });
            }

            await context.SaveChangesAsync();
        }

        await using var read = _db.NewContext();
        var store = new IdentityReadStore(read, InMemoryCache(_db.Clock), NullLogger<IdentityReadStore>.Instance);

        (await store.ListAccessLogAsync(null, null, account, new PageRequest(1, 10))).TotalCount.Should().Be(3);
        var recent = await store.ListAccessLogAsync(_db.Clock.GetUtcNow().UtcDateTime.AddMinutes(3), null, null, new PageRequest(1, 10));
        recent.Items.Should().HaveCount(2);
        recent.Items.First().AtUtc.Should().BeAfter(recent.Items.Last().AtUtc, "newest first");
        (await store.ListAccessLogAsync(null, _db.Clock.GetUtcNow().UtcDateTime.AddMinutes(1), null, new PageRequest(1, 1))).Items.Should().ContainSingle();
    }

    private static JobPlatform.BuildingBlocks.Infrastructure.Caching.InMemoryCacheStore InMemoryCache(TimeProvider clock) =>
        new(clock, Options.Create(new JobPlatform.BuildingBlocks.Infrastructure.Caching.CacheOptions()));
}
