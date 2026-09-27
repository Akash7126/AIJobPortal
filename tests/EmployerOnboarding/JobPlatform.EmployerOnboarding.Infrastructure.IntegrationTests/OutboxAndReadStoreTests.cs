using System.Text.Json.Nodes;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.EmployerOnboarding.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.IntegrationTests;

public class OutboxTests
{
    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-01")]
    public async Task Approve_WritesTheOutboxRowInTheSameSaveAsTheAggregate()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using var db = database.NewContext();
        var registration = EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Db.T0);
        registration.SubmitLevel2(Db.ValidIdentity(), Db.ValidLevel2(), new Actor(employerId, false));
        db.EmployerRegistrations.Add(registration);
        await db.SaveChangesAsync();

        registration.Approve(Db.Admin, Db.T0.AddMinutes(1));
        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync();
        (row.Type, row.Exchange, row.RoutingKey, row.Status).Should().Be(
            ("EmployerRegistrationApproved", "jobplatform.employer-onboarding.events", "employer-registration.approved.v1", OutboxStatus.Pending));
        var payload = JsonNode.Parse(row.Payload)!;
        payload["employerAccountId"]!.GetValue<Guid>().Should().Be(employerId);
        payload["actorId"]!.GetValue<Guid>().Should().Be(Db.Admin.Id);
    }

    [Fact]
    public async Task CompanyMediaAttach_WritesTheOutboxRow_ButRemoveDoesNot()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using var db = database.NewContext();
        var media = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo,
            new FileReference("k", "l.png", 10, "image/png", "h"), new Actor(employerId, false), Db.T0);
        db.CompanyMedia.Add(media);
        await db.SaveChangesAsync();

        media.Remove(new Actor(employerId, false), Db.T0);
        await db.SaveChangesAsync();

        var rows = await db.Set<OutboxMessage>().ToListAsync();
        rows.Should().ContainSingle(r => r.Type == "CompanyMediaAndDocumentCreated");
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNeitherTheAggregateNorTheOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        await db.BeginTransactionAsync();
        db.EmployerRegistrations.Add(EmployerRegistration.OpenFor(Guid.NewGuid(), Guid.NewGuid(), Db.T0));
        await db.SaveChangesAsync();
        await db.RollbackTransactionAsync();

        (await db.EmployerRegistrations.CountAsync()).Should().Be(0);
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }
}

public class ReadStoreTests
{
    [Fact]
    public async Task ListRegistrations_FiltersByStatusAndPages()
    {
        await using var database = Db.New();
        await using (var write = database.NewContext())
        {
            for (var i = 0; i < 3; i++)
            {
                var employerId = Guid.NewGuid();
                var registration = EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Db.T0.AddMinutes(i));
                if (i == 0)
                {
                    registration.SubmitLevel2(Db.ValidIdentity(), Db.ValidLevel2(), new Actor(employerId, false));
                    registration.Approve(Db.Admin, Db.T0.AddMinutes(i + 10));
                }

                write.EmployerRegistrations.Add(registration);
            }

            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new EmployerReadStore(read, new Adapters.LocalFileStorage(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));
        var approved = await store.ListRegistrationsAsync("Approved", new PageRequest(1, 10));
        var pending = await store.ListRegistrationsAsync("Pending", new PageRequest(1, 1));

        approved.TotalCount.Should().Be(1);
        pending.Should().Match<PagedResult<Application.EmployerRegistrationView>>(p => p.TotalCount == 2 && p.Items.Count == 1);
    }

    [Fact]
    public async Task GetCompanyPublicInfo_UsesPrimaryLogoAndLevel2()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using (var write = database.NewContext())
        {
            var registration = EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Db.T0);
            registration.SubmitLevel2(Db.ValidIdentity(), Db.ValidLevel2(), new Actor(employerId, false));
            write.EmployerRegistrations.Add(registration);
            var logo = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, new FileReference("key1", "l.png", 10, "image/png", "h"),
                new Actor(employerId, false), Db.T0);
            logo.SetAsPrimaryLogo(new Actor(employerId, false), Db.T0);
            write.CompanyMedia.Add(logo);
            await write.SaveChangesAsync();
        }

        await using var read = database.NewContext();
        var store = new EmployerReadStore(read, new Adapters.LocalFileStorage(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));
        var info = await store.GetCompanyPublicInfoAsync(employerId);

        info.Should().NotBeNull();
        info!.Name.Should().Be("Acme Ltd");
        info.LogoUrl.Should().NotBeNullOrEmpty();
        info.Industry.Should().Be("Software");
    }
}
