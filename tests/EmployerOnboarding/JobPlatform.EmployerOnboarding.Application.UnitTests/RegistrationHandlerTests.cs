using JobPlatform.EmployerOnboarding.Application.Commands.Registration;
using JobPlatform.EmployerOnboarding.Application.DTOs.Registration;
using JobPlatform.EmployerOnboarding.Application.Handlers.Registration;
using JobPlatform.EmployerOnboarding.Application.Queries.Registration;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;

namespace JobPlatform.EmployerOnboarding.Application.UnitTests;

public class RegistrationHandlerTests
{
    private static SubmitEmployerLevel2Command ValidSubmission() =>
        new("Acme Ltd", "CO-123", "REG-456", "https://acme.example", "Software", CompanySize.Small, "Ramallah", "Ramallah", null, "A company.");

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    public async Task SubmitEmployerLevel2Handler_ForOwnPendingRegistration_Succeeds()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        store.Registrations.Add(EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Kit.Clock().GetUtcNow().UtcDateTime));
        var handler = new SubmitEmployerLevel2Handler(store, Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(ValidSubmission(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Level2.Should().NotBeNull();
        result.Value.Identity!.CompanyId.Should().Be("CO-123");
    }

    [Fact]
    public async Task SubmitEmployerLevel2Handler_WithNoRegistrationYet_ReturnsNotFound()
    {
        var handler = new SubmitEmployerLevel2Handler(new FakeStore(), Kit.User(ActorType.Employer), Kit.Clock());

        var result = await handler.Handle(ValidSubmission(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-01")]
    public async Task ApproveEmployerRegistrationHandler_ApprovesAndOpensStanding()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        var registration = EmployerRegistration.OpenFor(Guid.NewGuid(), employerId, Kit.Clock().GetUtcNow().UtcDateTime);
        registration.SubmitLevel2(new CompanyIdentity("Acme", "CO-1", "REG-1"),
            new Level2Details("https://acme.example", "Software", CompanySize.Small, new Address("Ramallah", "Ramallah", null), "d"),
            new Domain.Common.Actor(employerId, false));
        store.Registrations.Add(registration);
        var handler = new ApproveEmployerRegistrationHandler(store, store, Kit.User(ActorType.Administrator), Kit.Clock());

        var result = await handler.Handle(new ApproveEmployerRegistrationCommand(registration.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        registration.Status.Should().Be(RegistrationStatus.Approved);
        store.Standings.Should().ContainSingle(s => s.EmployerAccountId == employerId && s.AdmissionApproved);
    }

    [Fact]
    public async Task ApproveEmployerRegistrationHandler_UnknownId_ReturnsNotFound()
    {
        var handler = new ApproveEmployerRegistrationHandler(new FakeStore(), new FakeStore(), Kit.User(ActorType.Administrator), Kit.Clock());

        var result = await handler.Handle(new ApproveEmployerRegistrationCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.NotFound);
    }

    [Fact]
    public async Task GetEmployerRegistrationHandler_UsesCurrentUsersAccountId()
    {
        var employerId = Guid.NewGuid();
        var expected = new EmployerRegistrationView(Guid.NewGuid(), employerId, "Pending", null, null, DateTime.UtcNow, null, null, Array.Empty<byte>());
        var store = new FakeReadStore { Registration = id => id == employerId ? expected : null };
        var handler = new GetEmployerRegistrationHandler(store, Kit.User(ActorType.Employer, employerId));

        var result = await handler.Handle(new GetEmployerRegistrationQuery(), CancellationToken.None);

        result.Value.Should().Be(expected);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    public async Task OpenEmployerRegistrationHandler_ForEmployerAccountApproved_OpensRegistrationAndStanding()
    {
        var store = new FakeStore();
        var handler = new OpenEmployerRegistrationHandler(store, store, store, Kit.Clock());
        var accountId = Guid.NewGuid();
        var evt = new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(), ActorType.Employer, 1);

        await handler.Handle(evt, CancellationToken.None);

        store.Registrations.Should().ContainSingle(r => r.EmployerAccountId == accountId && r.Status == RegistrationStatus.Pending);
        store.Standings.Should().ContainSingle(s => s.EmployerAccountId == accountId);
        store.KnownAccounts.Should().ContainSingle(a => a.AccountId == accountId);
    }

    [Fact]
    public async Task OpenEmployerRegistrationHandler_ForNonEmployerActor_IsIgnored()
    {
        var store = new FakeStore();
        var handler = new OpenEmployerRegistrationHandler(store, store, store, Kit.Clock());
        var evt = new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), ActorType.JobSeeker, 1);

        await handler.Handle(evt, CancellationToken.None);

        store.Registrations.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    public async Task OpenEmployerRegistrationHandler_RedeliveredForSameAccount_DoesNotDuplicate()
    {
        var store = new FakeStore();
        var handler = new OpenEmployerRegistrationHandler(store, store, store, Kit.Clock());
        var accountId = Guid.NewGuid();
        var evt = new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(), ActorType.Employer, 1);

        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt with { MessageId = Guid.NewGuid() }, CancellationToken.None);

        store.Registrations.Should().ContainSingle();
        store.Standings.Should().ContainSingle();
        store.KnownAccounts.Should().ContainSingle();
    }
}
