using JobPlatform.JobSeekerProfile.Application.Profile;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using DomainProfile = JobPlatform.JobSeekerProfile.Domain.Profile;

namespace JobPlatform.JobSeekerProfile.Application.UnitTests;

public class CreateProfileHandlerTests
{
    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    [Trait("AC", "AC-05")]
    public async Task Handle_FirstTime_CreatesProfile()
    {
        var store = new FakeStore();
        var ownerId = Guid.NewGuid();
        store.KnownAccounts.Add(KnownAccount.Create(ownerId, ActorType.JobSeeker, DateTime.UtcNow));
        var handler = new CreateProfileHandler(store, store, Kit.User(ActorType.JobSeeker, ownerId), Kit.Clock());

        var result = await handler.Handle(new CreateProfileCommand("Layla Haddad", "layla@example.org", "+970590000001", "Female"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Profiles.Should().ContainSingle().Which.OwnerAccountId.Should().Be(ownerId);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-03")]
    public async Task Handle_WhenProfileAlreadyExists_ReturnsConflict()
    {
        var store = new FakeStore();
        var ownerId = Guid.NewGuid();
        store.Profiles.Add(DomainProfile.Create(Guid.NewGuid(), ownerId, new FullName("Layla"), JobPlatform.SharedKernel.Common.ValueObjects.Email.Create("l@example.org"),
            JobPlatform.SharedKernel.Common.ValueObjects.MobileNumber.Create("+970590000001"), Gender.Female, true, ownerId, DateTime.UtcNow));
        var handler = new CreateProfileHandler(store, store, Kit.User(ActorType.JobSeeker, ownerId), Kit.Clock());

        var result = await handler.Handle(new CreateProfileCommand("Layla Haddad", "layla2@example.org", "+970590000002", "Female"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }
}

public class UpdateLevel1HandlerTests
{
    private static DomainProfile Seed(FakeStore store, Guid ownerId) =>
        DomainProfile.Create(Guid.NewGuid(), ownerId, new FullName("Layla"), JobPlatform.SharedKernel.Common.ValueObjects.Email.Create("l@example.org"),
            JobPlatform.SharedKernel.Common.ValueObjects.MobileNumber.Create("+970590000001"), Gender.Female, true, ownerId, DateTime.UtcNow);

    [Fact]
    [Trait("Story", "US-3.1.1-04")]
    [Trait("AC", "AC-03")]
    public async Task Handle_WithStaleIfMatch_ReturnsConflict()
    {
        var store = new FakeStore();
        var ownerId = Guid.NewGuid();
        var profile = Seed(store, ownerId);
        store.Profiles.Add(profile);
        var handler = new UpdateLevel1Handler(store, Kit.User(ActorType.JobSeeker, ownerId), Kit.Clock());
        var staleTag = ETag.From(new byte[] { 1, 2, 3, 4 });

        var result = await handler.Handle(new UpdateLevel1Command("New Name", "l@example.org", "+970590000001", "Female", staleTag), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(JobPlatform.JobSeekerProfile.Domain.Common.ErrorCodes.Conflict);
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-04")]
    [Trait("AC", "AC-01")]
    public async Task Handle_WithoutIfMatch_Succeeds()
    {
        var store = new FakeStore();
        var ownerId = Guid.NewGuid();
        store.Profiles.Add(Seed(store, ownerId));
        var handler = new UpdateLevel1Handler(store, Kit.User(ActorType.JobSeeker, ownerId), Kit.Clock());

        var result = await handler.Handle(new UpdateLevel1Command("New Name", "l@example.org", "+970590000001", "Female", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Profiles.Single().FullName.Value.Should().Be("New Name");
    }
}

public class UpdateLevel1ValidatorTests
{
    private readonly UpdateLevel1Validator _validator = new();

    [Theory]
    [Trait("Story", "US-3.1.1-04")]
    [InlineData("", "l@example.org", "+970590000001", "Female")]
    [InlineData("Layla", "not-an-email", "+970590000001", "Female")]
    [InlineData("Layla", "l@example.org", "not-a-number", "Female")]
    [InlineData("Layla", "l@example.org", "+970590000001", "Unknown")]
    public void Validate_RejectsMalformedInput(string fullName, string email, string mobile, string gender)
    {
        var result = _validator.Validate(new UpdateLevel1Command(fullName, email, mobile, gender, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsWellFormedInput()
    {
        var result = _validator.Validate(new UpdateLevel1Command("Layla Haddad", "l@example.org", "+970590000001", "Female", null));

        result.IsValid.Should().BeTrue();
    }
}
