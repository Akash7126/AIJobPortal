using FluentValidation.TestHelper;
using JobPlatform.JobPosting.Application.Commands.Interested;
using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Application.Queries.Postings;
using JobPlatform.JobPosting.Application.Validators.Interested;
using JobPlatform.JobPosting.Application.Validators.Postings;
using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.UnitTests;

public class ValidatorTests
{
    [Fact]
    [Trait("Story", "US-3.2.2-01")]
    [Trait("AC", "AC-03")]
    public void SearchJobPostingsValidator_SalaryMinGreaterThanMax_HasError()
    {
        var validator = new SearchJobPostingsValidator();

        var result = validator.TestValidate(new SearchJobPostingsQuery(null, null, null, 2000, 1000, null, null, null, null, null));

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.SearchInvalidField);
    }

    [Fact]
    public void SearchJobPostingsValidator_PageSizeTooLarge_HasError()
    {
        var validator = new SearchJobPostingsValidator();

        var result = validator.TestValidate(new SearchJobPostingsQuery(null, null, null, null, null, null, null, null, null, null, 1, 500));

        result.ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    [Trait("Story", "US-3.2.1-01")]
    [Trait("AC", "AC-03")]
    public void CreateJobPostingValidator_TooFewSkills_HasError()
    {
        var validator = new CreateJobPostingValidator();
        var command = new CreateJobPostingCommand("ar", "en", new string('a', 30), new string('b', 30), Array.Empty<string>(), "cat", "FullTime", null,
            null, "Hybrid", null, null, null, null, null, null, null, null, DateTime.UtcNow.AddDays(10), true, null, null, null);

        var result = validator.TestValidate(command);

        result.Errors.Should().Contain(e => e.ErrorCode == "VAL.Skills.OutOfRange");
    }

    [Fact]
    public void AddInterestedListEntryValidator_FilterWithoutAnyCriterion_HasError()
    {
        var validator = new AddInterestedListEntryValidator();

        var result = validator.TestValidate(new AddInterestedListEntryCommand("Filter", null, null, null, null, null, null, null, null));

        result.Errors.Should().Contain(e => e.ErrorCode == "VAL.Criteria.AtLeastOneRequired");
    }
}

public class EventMapperTests
{
    private readonly Events.JobPostingEventMapper _mapper = new();
    private static readonly SharedKernel.Messaging.DomainEventContext Context = new("id", 1, Guid.NewGuid(), null);

    [Fact]
    public void Map_JobPostingCreated_ProducesIntegrationEventWithSameId()
    {
        var domainEvent = new JobPostingCreatedDomainEvent(DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), JobPostingStatus.Draft, "Title",
            "cat", new[] { "csharp" }, VisibilityScope.Public, JobSourceType.Employer, null, null, null);

        var integrationEvent = _mapper.Map(domainEvent, Context);

        integrationEvent.Should().BeOfType<SharedKernel.IntegrationEvents.JobPosting.JobPostingCreatedIntegrationEvent>()
            .Which.JobPostingId.Should().Be(domainEvent.JobPostingId);
    }

    [Fact]
    public void Map_UnknownEvent_ReturnsNull()
    {
        _mapper.Map(new UnrelatedDomainEvent(DateTime.UtcNow), Context).Should().BeNull();
    }

    private sealed record UnrelatedDomainEvent(DateTime OccurredOnUtc) : SharedKernel.Domain.DomainEvent(OccurredOnUtc);
}

public class InboxHandlerTests
{
    private readonly FakeStore _store = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Kit.Clock();

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-01")]
    public async Task ImportExternalJob_New_CreatesActivePosting()
    {
        var handler = new Events.ImportExternalJobHandler(_store, new Events.SavedSearchMatchEvaluator(_store), _clock);
        var e = new SharedKernel.IntegrationEvents.ExternalIntegration.JobDataImportedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PJ-1", "SJ-1", "Imported Title", "Imported summary text.", new[] { "python" }, "FullTime",
            "Physical", DateTime.UtcNow.AddDays(20), "Gaza", "https://source.example/1", "public", false, 1);

        await handler.Handle(e, default);

        var posting = _store.Postings.Should().ContainSingle().Which;
        posting.Status.Should().Be(JobPostingStatus.Active);
        posting.Source.PlatformJobId.Should().Be("PJ-1");
    }

    [Fact]
    public async Task ImportExternalJob_Twice_UpdatesInsteadOfDuplicating()
    {
        var handler = new Events.ImportExternalJobHandler(_store, new Events.SavedSearchMatchEvaluator(_store), _clock);
        var e = new SharedKernel.IntegrationEvents.ExternalIntegration.JobDataImportedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PJ-2", "SJ-2", "Title", "Summary text long enough.", new[] { "python" }, "FullTime", "Physical",
            DateTime.UtcNow.AddDays(20), "Gaza", null, "public", false, 1);
        await handler.Handle(e, default);

        await handler.Handle(e with { Title = "Updated Title" }, default);

        _store.Postings.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-09")]
    [Trait("AC", "AC-01")]
    public async Task ApplyAdminSuspension_ForcesPausedAndSetsFlag()
    {
        var employerId = Guid.NewGuid();
        var posting = TestBuild.Active(employerId);
        _store.Postings.Add(posting);
        var handler = new Events.ApplyAdminSuspensionHandler(_store, _clock);
        var e = new SharedKernel.IntegrationEvents.PlatformAdministration.JobOfferingSuspendedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(),
            null, Guid.NewGuid(), posting.Id, Guid.NewGuid(), "policy violation", 1);

        await handler.Handle(e, default);

        posting.AdminSuspended.Should().BeTrue();
        posting.Status.Should().Be(JobPostingStatus.Paused);
    }
}
