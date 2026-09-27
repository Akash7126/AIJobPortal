using JobPlatform.EmployerOnboarding.Application.Events;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.EmployerOnboarding.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.IntegrationTests;

internal static class Db
{
    public static readonly DateTime T0 = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    public static readonly Actor Admin = new(Guid.NewGuid(), true);

    public static SqliteTestDatabase<EmployerOnboardingDbContext> New() =>
        new(o => new EmployerOnboardingDbContext(o), new EmployerOnboardingEventMapper());

    public static Level2Details ValidLevel2() =>
        new("https://acme.example", "Software", CompanySize.Small, new Address("Ramallah", "Ramallah", "Main St"), "A company.");

    public static CompanyIdentity ValidIdentity() => new("Acme Ltd", "CO-123", "REG-456");
}

public class RepositoryTests
{
    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    public async Task EmployerRegistration_RoundTripsIdentityAndLevel2Json()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var registration = EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Db.T0);
            registration.SubmitLevel2(Db.ValidIdentity(), Db.ValidLevel2(), new Actor(employerId, false));
            write.EmployerRegistrations.Add(registration);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new EmployerRegistrationRepository(read).GetByEmployerAsync(employerId);
        loaded!.CompanyIdentity!.CompanyId.Should().Be("CO-123");
        loaded.Level2!.Address.City.Should().Be("Ramallah");
        loaded.Status.Should().Be(RegistrationStatus.Pending);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-01")]
    public async Task EmployerRegistration_SameEmployerAccountTwice_ViolatesTheUniqueIndex()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using var db = database.NewContext();
        db.EmployerRegistrations.Add(EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Db.T0));
        await db.SaveChangesAsync();
        db.EmployerRegistrations.Add(EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Db.T0));

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-04")]
    public async Task CompanyMedia_RoundTripsFileAndOnlyOnePrimaryLogoSurvivesARead()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        var actor = new Actor(employerId, false);
        await using (var write = database.NewContext())
        {
            var logo = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, new FileReference("k", "l.png", 10, "image/png", "h1"), actor, Db.T0);
            logo.SetAsPrimaryLogo(actor, Db.T0);
            write.CompanyMedia.Add(logo);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var media = await new CompanyMediaRepository(read).ListByEmployerAsync(employerId);
        media.Should().ContainSingle().Which.File.Sha256.Should().Be("h1");
        media.Single().IsPrimaryLogo.Should().BeTrue();
    }

    [Fact]
    public async Task CompanyMedia_GetByHash_OnlyFindsNonRemovedItems()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        var actor = new Actor(employerId, false);
        var media = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, new FileReference("k", "l.png", 10, "image/png", "hash-x"), actor, Db.T0);
        await using (var write = database.NewContext())
        {
            write.CompanyMedia.Add(media);
            await write.SaveChangesAsync();
        }

        (await new CompanyMediaRepository(database.NewContext()).GetByHashAsync(employerId, "hash-x")).Should().NotBeNull();

        await using (var remove = database.NewContext())
        {
            var tracked = await remove.CompanyMedia.SingleAsync();
            tracked.Remove(actor, Db.T0);
            await remove.SaveChangesAsync();
        }

        (await new CompanyMediaRepository(database.NewContext()).GetByHashAsync(employerId, "hash-x")).Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-04")]
    [Trait("AC", "AC-04")]
    public async Task EmployerStanding_RoundTripsBadgeAudit()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var standing = EmployerStanding.OpenFor(Guid.NewGuid(), employerId);
            standing.MarkAdmissionApproved(Db.T0);
            standing.MarkVerified(messageId, 1, Db.T0);
            write.EmployerStandings.Add(standing);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var loaded = await new EmployerStandingRepository(read).GetAsync(employerId);
        loaded!.IsVerified.Should().BeTrue();
        loaded.AdmissionApproved.Should().BeTrue();
        loaded.BadgeAudit.Should().ContainSingle().Which.CausedByMessageId.Should().Be(messageId);
    }

    [Fact]
    public async Task KnownAccount_RoundTrips()
    {
        await using var database = Db.New();
        var accountId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            write.KnownAccounts.Add(new KnownAccount(accountId, Db.T0));
            await write.SaveChangesAsync();
        }

        (await new KnownAccountRepository(database.NewContext()).GetAsync(accountId)).Should().NotBeNull();
        (await new KnownAccountRepository(database.NewContext()).GetAsync(Guid.NewGuid())).Should().BeNull();
    }
}
