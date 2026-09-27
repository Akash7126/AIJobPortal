using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Application.UnitTests;

public class EmployerVerificationHandlerTests
{
    private static RequestEmployerVerificationCommand ValidCommand() => new("REG-1", "VAT-1", "+970591234567", null);

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-01")]
    public async Task RequestEmployerVerification_KnownEmployerAndAutomaticMatch_IsVerified()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        store.KnownAccounts.Add(new KnownAccount(employerId, ActorType.Employer, Kit.Clock().GetUtcNow().UtcDateTime, 1));
        var mol = new FakeMolRegistryClient { NextEmployerOutcome = SourceCallOutcome.Match };
        var handler = new RequestEmployerVerificationHandler(store, store, mol, Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be("Verified");
        result.Value.Method.Should().Be("Automatic");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-02")]
    public async Task RequestEmployerVerification_NoAutomaticMatch_EscalatesToManualReview()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        store.KnownAccounts.Add(new KnownAccount(employerId, ActorType.Employer, Kit.Clock().GetUtcNow().UtcDateTime, 1));
        var mol = new FakeMolRegistryClient { NextEmployerOutcome = SourceCallOutcome.NoMatch };
        var handler = new RequestEmployerVerificationHandler(store, store, mol, Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.Value.State.Should().Be("PendingManualReview");
        result.Value.Method.Should().Be("ManualMoL");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-03")]
    public async Task RequestEmployerVerification_SourceUnavailable_EscalatesWithTimeoutAttempt()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        store.KnownAccounts.Add(new KnownAccount(employerId, ActorType.Employer, Kit.Clock().GetUtcNow().UtcDateTime, 1));
        var mol = new FakeMolRegistryClient { NextEmployerOutcome = SourceCallOutcome.Unavailable, NextErrorCode = "E-ERPM-UPSTREAM-TIMEOUT" };
        var handler = new RequestEmployerVerificationHandler(store, store, mol, Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.Value.State.Should().Be("PendingManualReview");
        result.Value.Attempts.Should().ContainSingle().Which.Outcome.Should().Be("Timeout");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task RequestEmployerVerification_UnknownAccount_ReturnsAccountNotEmployerConflict()
    {
        var handler = new RequestEmployerVerificationHandler(new FakeStore(), new FakeStore(), new FakeMolRegistryClient(),
            Kit.User(ActorType.Employer), Kit.Clock());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.AccountNotEmployer);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task RequestEmployerVerification_AlreadyActive_ReturnsConflict()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        store.KnownAccounts.Add(new KnownAccount(employerId, ActorType.Employer, Kit.Clock().GetUtcNow().UtcDateTime, 1));
        store.Add(EmployerVerification.Request(Guid.NewGuid(), employerId, new Submission("R", "V", "+970591234567")));
        var handler = new RequestEmployerVerificationHandler(store, store, new FakeMolRegistryClient(), Kit.User(ActorType.Employer, employerId),
            Kit.Clock());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.AlreadyDecided);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task DecideEmployerVerificationManually_Approve_TransitionsToVerified()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        var verification = EmployerVerification.Request(Guid.NewGuid(), employerId, new Submission("R", "V", "+970591234567"));
        verification.EscalateToManualReview("no match");
        store.Add(verification);
        var reviewerId = Guid.NewGuid();
        var handler = new DecideEmployerVerificationManuallyHandler(store, Kit.User(ActorType.Administrator, reviewerId), Kit.Clock());

        var result = await handler.Handle(new DecideEmployerVerificationManuallyCommand(verification.Id, ManualDecision.Approve, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        verification.State.Should().Be(VerificationState.Verified);
        verification.DecidedBy.Should().Be(reviewerId);
    }

    [Fact]
    public async Task GetEmployerVerification_ByOtherEmployer_ReturnsForbidden()
    {
        var view = new EmployerVerificationView(Guid.NewGuid(), Guid.NewGuid(), "Pending", "Automatic", 0, null, null, null,
            Array.Empty<VerificationAttemptView>(), Array.Empty<byte>());
        var store = new FakeReadStore { EmployerVerification = _ => view };
        var handler = new GetEmployerVerificationHandler(store, Kit.User(ActorType.Employer, Guid.NewGuid()));

        var result = await handler.Handle(new GetEmployerVerificationQuery(view.EmployerVerificationId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(Domain.Common.ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task GetEmployerVerification_ByAdministrator_Succeeds()
    {
        var view = new EmployerVerificationView(Guid.NewGuid(), Guid.NewGuid(), "Pending", "Automatic", 0, null, null, null,
            Array.Empty<VerificationAttemptView>(), Array.Empty<byte>());
        var store = new FakeReadStore { EmployerVerification = _ => view };
        var handler = new GetEmployerVerificationHandler(store, Kit.User(ActorType.Administrator));

        var result = await handler.Handle(new GetEmployerVerificationQuery(view.EmployerVerificationId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
