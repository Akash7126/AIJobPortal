using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Application.Events;
using JobPlatform.JobPosting.Application.Handlers.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Application.UnitTests;

public class PostingHandlerTests
{
    private readonly FakeStore _store = new();
    private readonly FakeSchemaValidator _schema = new();
    private readonly FakeEmployerStanding _standing = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Kit.Clock();

    private static CreateJobPostingCommand CreateCommand(string[]? skills = null) => new(
        "مهندس", "Engineer", "وصف طويل بما فيه الكفاية لتجاوز الحد الأدنى للتحقق من الصحة في هذا الاختبار.",
        "A sufficiently long summary that satisfies the minimum length validation rule for this test.", skills ?? new[] { "csharp" },
        "software-development", "FullTime", "Bachelor", null, "Hybrid", "Ramallah", "Ramallah", 1000, 2000, "ILS", 1, 3, new[] { "en" },
        DateTime.UtcNow.AddDays(30), true, "https://example.com", null, null);

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Create_NewDraft_AddsPostingAndReturnsNotExisting()
    {
        var user = Kit.User(id: Guid.NewGuid());
        var handler = new CreateJobPostingHandler(_store, _schema, user, _clock);

        var result = await handler.Handle(CreateCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Existing.Should().BeFalse();
        _store.Postings.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Create_IdenticalDraftTwice_ReturnsExisting_DoesNotDuplicate()
    {
        var employerId = Guid.NewGuid();
        var user = Kit.User(id: employerId);
        var handler = new CreateJobPostingHandler(_store, _schema, user, _clock);
        await handler.Handle(CreateCommand(), default);

        var second = await handler.Handle(CreateCommand(), default);

        second.Value.Existing.Should().BeTrue();
        _store.Postings.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.2.1-02")]
    [Trait("AC", "AC-02")]
    public async Task Create_WhenTaxonomyUnavailable_ThrowsUpstreamTimeout()
    {
        _schema.ShouldTimeOut = true;
        var handler = new CreateJobPostingHandler(_store, _schema, Kit.User(), _clock);

        var act = () => handler.Handle(CreateCommand(), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.UpstreamTimeout);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-02")]
    [Trait("AC", "AC-03")]
    public async Task Create_WhenSchemaInvalid_ThrowsInvalidField()
    {
        _schema.Errors = new[] { "unknown skill" };
        var handler = new CreateJobPostingHandler(_store, _schema, Kit.User(), _clock);

        var act = () => handler.Handle(CreateCommand(), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be(ErrorCodes.InvalidField);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-03")]
    [Trait("AC", "AC-04")]
    public async Task Update_WithStaleIfMatch_ReturnsPreconditionFailed()
    {
        var employerId = Guid.NewGuid();
        var posting = TestBuild.Draft(employerId);
        _store.Postings.Add(posting);
        var handler = new UpdateJobPostingHandler(_store, _schema, Kit.User(id: employerId), _clock);
        var staleETag = "\"" + Convert.ToBase64String(new byte[] { 1, 2, 3 }) + "\"";

        var command = new UpdateJobPostingCommand(posting.Id, "ar", "en", "a".PadRight(30), "b".PadRight(30), new[] { "csharp" }, "software-development",
            "FullTime", null, null, "Hybrid", null, null, null, null, null, null, null, null, DateTime.UtcNow.AddDays(20), true, null, null, "Public", null,
            staleETag);

        var result = await handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("E-PRECONDITION-FAILED");
    }

    [Fact]
    [Trait("Story", "US-3.2.1-04")]
    [Trait("AC", "AC-01")]
    public async Task Renew_ExpiredPosting_MovesToActive()
    {
        var employerId = Guid.NewGuid();
        var posting = TestBuild.Active(employerId);
        posting.Expire(new Actor(employerId, false, false), _clock.GetUtcNow().UtcDateTime);
        _store.Postings.Add(posting);
        var handler = new RenewJobPostingHandler(_store, new SavedSearchMatchEvaluator(_store), Kit.User(id: employerId), _clock);

        var result = await handler.Handle(new RenewJobPostingCommand(posting.Id, DateTime.UtcNow.AddDays(60)), default);

        result.IsSuccess.Should().BeTrue();
        posting.Status.Should().Be(JobPostingStatus.Active);
    }

    [Fact]
    [Trait("Story", "US-3.2.4-01")]
    [Trait("AC", "AC-01")]
    public async Task UpdateStatus_PublishWhenEmployerNotApproved_ReturnsForbidden()
    {
        var employerId = Guid.NewGuid();
        var posting = TestBuild.Draft(employerId);
        _store.Postings.Add(posting);
        _standing.Approved = false;
        var handler = new UpdateJobPostingStatusHandler(_store, _standing, new SavedSearchMatchEvaluator(_store), Kit.User(id: employerId), _clock);

        var result = await handler.Handle(new UpdateJobPostingStatusCommand(posting.Id, "Active"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(SharedKernel.Application.Results.ErrorType.Forbidden);
    }

    [Fact]
    [Trait("Story", "US-3.2.4-01")]
    [Trait("AC", "AC-02")]
    public async Task UpdateStatus_PublishWhenApproved_MovesToActive()
    {
        var employerId = Guid.NewGuid();
        var posting = TestBuild.Draft(employerId);
        _store.Postings.Add(posting);
        var handler = new UpdateJobPostingStatusHandler(_store, _standing, new SavedSearchMatchEvaluator(_store), Kit.User(id: employerId), _clock);

        var result = await handler.Handle(new UpdateJobPostingStatusCommand(posting.Id, "Active"), default);

        result.IsSuccess.Should().BeTrue();
        posting.Status.Should().Be(JobPostingStatus.Active);
    }

    [Fact]
    public async Task ExpireDuePostings_MovesDuePostingsToExpired()
    {
        var posting = TestBuild.Active(Guid.NewGuid(), deadline: _clock.GetUtcNow().UtcDateTime.AddDays(5));
        _store.Postings.Add(posting);
        _clock.Advance(TimeSpan.FromDays(6));
        var handler = new ExpireDuePostingsHandler(_store, _clock);

        var result = await handler.Handle(new ExpireDuePostingsCommand(50), default);

        result.Value.Should().Be(1);
        posting.Status.Should().Be(JobPostingStatus.Expired);
    }
}

/// <summary>Builds aggregates directly for handler tests without going through a command.</summary>
internal static class TestBuild
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    public static JobPostingFields Fields(DateTime? deadline = null) => new(
        new SharedKernel.Common.ValueObjects.LocalizedText("عنوان", "Title"),
        new SharedKernel.Common.ValueObjects.LocalizedText("وصف طويل بما فيه الكفاية لتجاوز الحد الأدنى.", "A sufficiently long summary for validation."),
        new[] { "csharp" }, "software-development", ContractType.FullTime, EducationLevel.Bachelor, Array.Empty<string>(), WorkFormat.Hybrid,
        JobLocation.Create("Ramallah", "Ramallah"), SalaryRange.Create(1000, 2000, "ILS"), 1, 3, new[] { "en" },
        ApplicationDeadline.Create(deadline ?? At.AddDays(30), true), null, null);

    public static JobPlatform.JobPosting.Domain.JobPosting Draft(Guid employerId, DateTime? deadline = null) =>
        JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(employerId, Fields(deadline), new Actor(employerId, false, false), 1,
            ContentHasher.Hash(Fields(deadline)), At);

    public static JobPlatform.JobPosting.Domain.JobPosting Active(Guid employerId, DateTime? deadline = null)
    {
        var posting = Draft(employerId, deadline);
        posting.Publish(new Actor(employerId, false, false), At);
        return posting;
    }
}
