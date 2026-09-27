using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;

namespace JobPlatform.GovernmentIntegration.Application.UnitTests;

public class KnownAccountsHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    public async Task RecordKnownAccount_NewAccount_IsAdded()
    {
        var store = new FakeStore();
        var handler = new RecordKnownAccountHandler(store);
        var accountId = Guid.NewGuid();
        var evt = new AccountCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(), ActorType.Employer, 1);

        await handler.Handle(evt, CancellationToken.None);

        store.KnownAccounts.Should().ContainSingle(a => a.AccountId == accountId && a.ActorType == ActorType.Employer);
    }

    [Fact]
    public async Task RecordKnownAccount_Redelivered_DoesNotDuplicate()
    {
        var store = new FakeStore();
        var handler = new RecordKnownAccountHandler(store);
        var accountId = Guid.NewGuid();
        var evt = new AccountCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(), ActorType.Employer, 1);

        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt with { MessageId = Guid.NewGuid() }, CancellationToken.None);

        store.KnownAccounts.Should().ContainSingle();
    }
}
