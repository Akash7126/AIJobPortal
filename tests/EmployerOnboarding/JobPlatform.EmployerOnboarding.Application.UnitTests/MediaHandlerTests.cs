using JobPlatform.EmployerOnboarding.Application.Commands.Media;
using JobPlatform.EmployerOnboarding.Application.Handlers.Media;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.EmployerOnboarding.Application.UnitTests;

public class MediaHandlerTests
{
    private static byte[] Bytes(string content = "hello") => System.Text.Encoding.UTF8.GetBytes(content);

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-01")]
    public async Task AttachCompanyMediaHandler_NewFile_StoresAndReturnsView()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        var storage = new FakeFileStorage();
        var handler = new AttachCompanyMediaHandler(store, storage, new FakeMalwareScanner(), Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(new AttachCompanyMediaCommand(MediaKind.Logo, "logo.png", "image/png", 5, Bytes()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.Media.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    [Trait("AC", "AC-03")]
    public async Task AttachCompanyMediaHandler_SameContentTwice_ReturnsExistingWithoutDuplicating()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        var handler = new AttachCompanyMediaHandler(store, new FakeFileStorage(), new FakeMalwareScanner(), Kit.User(ActorType.Employer, employerId), Kit.Clock());
        var command = new AttachCompanyMediaCommand(MediaKind.Logo, "logo.png", "image/png", 5, Bytes());

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        store.Media.Should().ContainSingle();
        second.Value.CompanyMediaId.Should().Be(first.Value.CompanyMediaId);
    }

    [Fact]
    public async Task AttachCompanyMediaHandler_ScannerFlagsFile_ReturnsBusinessRuleError()
    {
        var handler = new AttachCompanyMediaHandler(new FakeStore(), new FakeFileStorage(), new FakeMalwareScanner { NextResultClean = false },
            Kit.User(ActorType.Employer), Kit.Clock());

        var result = await handler.Handle(new AttachCompanyMediaCommand(MediaKind.Logo, "logo.png", "image/png", 5, Bytes()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveCompanyMediaHandler_MarksRemoved()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        var media = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, new FileReference("k", "f.png", 5, "image/png", "h"),
            new Domain.Common.Actor(employerId, false), Kit.Clock().GetUtcNow().UtcDateTime);
        store.Media.Add(media);
        var handler = new RemoveCompanyMediaHandler(store, Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(new RemoveCompanyMediaCommand(media.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        media.IsRemoved.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.2-06")]
    public async Task SetPrimaryLogoHandler_UnsetsThePreviousPrimary()
    {
        var employerId = Guid.NewGuid();
        var store = new FakeStore();
        var now = Kit.Clock().GetUtcNow().UtcDateTime;
        var actor = new Domain.Common.Actor(employerId, false);
        var first = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, new FileReference("k1", "a.png", 5, "image/png", "h1"), actor, now);
        var second = CompanyMediaAndDocument.Attach(Guid.NewGuid(), employerId, MediaKind.Logo, new FileReference("k2", "b.png", 5, "image/png", "h2"), actor, now);
        first.SetAsPrimaryLogo(actor, now);
        store.Media.AddRange(new[] { first, second });
        var handler = new SetPrimaryLogoHandler(store, Kit.User(ActorType.Employer, employerId), Kit.Clock());

        var result = await handler.Handle(new SetPrimaryLogoCommand(second.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        first.IsPrimaryLogo.Should().BeFalse();
        second.IsPrimaryLogo.Should().BeTrue();
    }
}
