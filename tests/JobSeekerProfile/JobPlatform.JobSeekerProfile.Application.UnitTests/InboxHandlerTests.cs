using JobPlatform.JobSeekerProfile.Application.Inbox;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using DomainProfile = JobPlatform.JobSeekerProfile.Domain.Profile;

namespace JobPlatform.JobSeekerProfile.Application.UnitTests;

public class RecordKnownAccountHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    public async Task Handle_JobSeekerApproved_UpsertsKnownAccount()
    {
        var store = new FakeStore();
        var accountId = Guid.NewGuid();
        var handler = new RecordKnownAccountHandler(store, Kit.Clock());

        await handler.Handle(new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, accountId, Guid.NewGuid(),
            ActorType.JobSeeker, 1), CancellationToken.None);

        store.KnownAccounts.Should().ContainSingle().Which.AccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task Handle_NonJobSeekerActor_IsIgnored()
    {
        var store = new FakeStore();
        var handler = new RecordKnownAccountHandler(store, Kit.Clock());

        await handler.Handle(new AccountApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            ActorType.Employer, 1), CancellationToken.None);

        store.KnownAccounts.Should().BeEmpty();
    }
}

public class ApplyExtractedProfileDataHandlerTests
{
    private static DomainProfile Seed(FakeStore store, Guid profileId)
    {
        var profile = DomainProfile.Create(profileId, Guid.NewGuid(), new FullName("Layla"), JobPlatform.SharedKernel.Common.ValueObjects.Email.Create("l@example.org"),
            JobPlatform.SharedKernel.Common.ValueObjects.MobileNumber.Create("+970590000001"), Gender.Female, true, Guid.NewGuid(), DateTime.UtcNow);
        store.Profiles.Add(profile);
        return profile;
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    [Trait("AC", "AC-04")]
    public async Task Handle_FirstDelivery_MergesSkills()
    {
        var store = new FakeStore();
        var profileId = Guid.NewGuid();
        Seed(store, profileId);
        var handler = new ApplyExtractedProfileDataHandler(store, store, Kit.Clock());
        var messageId = Guid.NewGuid();

        await handler.Handle(new ResumeParsedDataComputedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, messageId, Guid.NewGuid(),
            profileId, "en", "Completed", new[] { "C#" }, Array.Empty<string>(), 3, 1), CancellationToken.None);

        store.Profiles.Single().Skills.Should().ContainSingle().Which.Name.Should().Be("C#");
        store.Processed.Should().ContainSingle().Which.ResumeParsedDataId.Should().Be(messageId);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    [Trait("AC", "AC-04")]
    public async Task Handle_DuplicateDelivery_IsIgnored()
    {
        var store = new FakeStore();
        var profileId = Guid.NewGuid();
        Seed(store, profileId);
        var handler = new ApplyExtractedProfileDataHandler(store, store, Kit.Clock());
        var messageId = Guid.NewGuid();
        var integrationEvent = new ResumeParsedDataComputedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, messageId, Guid.NewGuid(),
            profileId, "en", "Completed", new[] { "C#" }, Array.Empty<string>(), 3, 1);
        await handler.Handle(integrationEvent, CancellationToken.None);

        await handler.Handle(integrationEvent, CancellationToken.None);

        store.Profiles.Single().Skills.Should().ContainSingle("the second delivery of the same resumeParsedDataId must be a no-op");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-11")]
    public async Task Handle_OutOfOrder_ProfileNotYetCreated_ThrowsSoTheInboxRetries()
    {
        var store = new FakeStore();
        var handler = new ApplyExtractedProfileDataHandler(store, store, Kit.Clock());

        var act = () => handler.Handle(new ResumeParsedDataComputedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "en", "Completed", new[] { "C#" }, Array.Empty<string>(), 3, 1), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

public class DeactivateProfileHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.1.1-07")]
    public async Task Handle_AccountSuspended_DeactivatesProfileAndLink()
    {
        var store = new FakeStore();
        var ownerId = Guid.NewGuid();
        var profile = DomainProfile.Create(Guid.NewGuid(), ownerId, new FullName("Layla"), JobPlatform.SharedKernel.Common.ValueObjects.Email.Create("l@example.org"),
            JobPlatform.SharedKernel.Common.ValueObjects.MobileNumber.Create("+970590000001"), Gender.Female, true, ownerId, DateTime.UtcNow);
        store.Profiles.Add(profile);
        var link = Domain.ProfileShareLink.Generate(Guid.NewGuid(), profile.Id, ownerId, sharingActivated: true, DateTime.UtcNow);
        store.ShareLinks.Add(link);
        var handler = new DeactivateProfileHandler(store, store, store, Kit.Clock());

        await handler.Handle(new AccountSuspendedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, ownerId, Guid.NewGuid(),
            ActorType.JobSeeker, "test", "Deactivated", 1), CancellationToken.None);

        store.Profiles.Single().Status.Should().Be(ProfileStatus.Deactivated);
        store.ShareLinks.Single().IsActive.Should().BeFalse();
    }
}
