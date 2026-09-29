using FluentValidation.TestHelper;
using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.Exports;
using JobPlatform.AuditLogging.Application.Validators.AuditLog;
using JobPlatform.AuditLogging.Application.Validators.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.AuditLogging.Application.UnitTests;

public class QueryHandlerTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly PagedResult<AuditEntryDto> EmptyPage = new(Array.Empty<AuditEntryDto>(), 1, 20, 0);

    private static AuditLogQueryHarness Harness(ActorType? actor, Guid? id)
    {
        var store = Substitute.For<IAuditReadStore>();
        store.ListEntriesAsync(default!, default!, default).ReturnsForAnyArgs(EmptyPage);
        return new AuditLogQueryHarness(store, Users.Of(actor, id));
    }

    private sealed record AuditLogQueryHarness(IAuditReadStore Store, JobPlatform.SharedKernel.Application.Ports.ICurrentUser User);

    private static IRequestHandler<TQuery, TResult> Handler<TQuery, TResult>(object harness) where TQuery : IQuery<TResult>
    {
        var h = (AuditLogQueryHarness)harness;
        return new RequestHandlerSet(typeof(ApplicationAssembly).Assembly, h.Store, h.User).For<TQuery, TResult>();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-06")]
    [Trait("AC", "AC-02")]
    public async Task PartnerLog_ReadsOnlyTheCallersOwnEntriesOfTheCategory()
    {
        var h = Harness(ActorType.ExternalJobSite, Me);

        var result = await Handler<ListApiResponseLogQuery, PagedResult<AuditEntryDto>>(h).Handle(new ListApiResponseLogQuery(null, null, "failure"), default);

        result.IsSuccess.Should().BeTrue();
        await h.Store.Received(1).ListEntriesAsync(Arg.Is<EntryFilter>(f =>
            f.Categories.SequenceEqual(new[] { AuditCategory.ApiCall }) && f.OwnerId == Me && f.Outcome == AuditOutcome.Failure && !f.IncludeArchived), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PartnerLog_ForAnotherActorType_IsForbiddenWithThePartnerCode()
    {
        var h = Harness(ActorType.Employer, Me);

        var act = () => Handler<ListSubmissionLogQuery, PagedResult<AuditEntryDto>>(h).Handle(new ListSubmissionLogQuery(null, null, null), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-TPJPRI-FORBIDDEN");
        await h.Store.DidNotReceiveWithAnyArgs().ListEntriesAsync(default!, default!, default);
    }

    [Fact]
    public async Task AdminLog_IsNotFilteredByOwner()
    {
        var h = Harness(ActorType.Administrator, Me);

        await Handler<ListAccessLogQuery, PagedResult<AuditEntryDto>>(h).Handle(new ListAccessLogQuery(null, null, null), default);

        await h.Store.Received(1).ListEntriesAsync(Arg.Is<EntryFilter>(f => f.OwnerId == null && f.Categories.Single() == AuditCategory.Access), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ActorType.Employer)]
    [InlineData(ActorType.JobSeeker)]
    [InlineData(ActorType.ExternalJobSite)]
    public async Task AdminLog_ForNonAdministrators_IsForbidden(ActorType actor)
    {
        var h = Harness(actor, Me);

        var act = () => Handler<ListAdminAuditLogQuery, PagedResult<AuditEntryDto>>(h).Handle(new ListAdminAuditLogQuery(null, null, null), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }

    [Fact]
    public async Task JobAuditTrail_FiltersBySubject()
    {
        var h = Harness(ActorType.Administrator, Me);

        await Handler<GetJobAuditTrailQuery, PagedResult<AuditEntryDto>>(h).Handle(new GetJobAuditTrailQuery("PJ-1", null, null, null), default);

        await h.Store.Received(1).ListEntriesAsync(Arg.Is<EntryFilter>(f => f.SubjectId == "PJ-1" && f.Categories.Single() == AuditCategory.JobAudit), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Story", "US-3.1.3-11")]
    [Trait("AC", "AC-02")]
    public async Task UsageStatistics_EndBeforeStart_IsInvalidFieldAndNeverReadsTheStore()
    {
        var store = Substitute.For<IAuditReadStore>();
        var handlers = Handlers(store, Users.Of(ActorType.ExternalJobSite, Me), new FakeTimeProvider());

        var act = () => handlers.Handle(new GetIntegrationUsageStatisticsQuery(new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 1)), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-TPJPRI-INVALID-FIELD");
        await store.DidNotReceiveWithAnyArgs().GetUsageAsync(default, default!, default);
    }

    [Fact]
    public async Task IntegrationStatus_UsesTheCallersIdAndTodaysDate()
    {
        var store = Substitute.For<IAuditReadStore>();
        store.GetIntegrationStatusAsync(default, default, default).ReturnsForAnyArgs(new IntegrationStatusDto("Healthy", 0, 1, 0, 0, null, 3));
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var handlers = Handlers(store, Users.Of(ActorType.ExternalJobSite, Me), clock);

        var result = await handlers.Handle(new GetIntegrationStatusDashboardQuery(), default);

        result.Value.Health.Should().Be("Healthy");
        await store.Received(1).GetIntegrationStatusAsync(Me, new DateOnly(2026, 6, 15), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Story", "US-3.2.4-02")]
    [Trait("AC", "AC-02")]
    public async Task JobStatusHistory_OfAnotherEmployersJob_IsForbidden()
    {
        var owner = Guid.NewGuid();
        var store = Substitute.For<IAuditReadStore>();
        store.GetJobStatusHistoryAsync(default, default).ReturnsForAnyArgs(new JobHistoryOwnerView(owner, Array.Empty<JobStatusHistoryDto>()));
        var handlers = Handlers(store, Users.Of(ActorType.Employer, Me));

        var act = () => handlers.Handle(new GetJobStatusHistoryQuery(Guid.NewGuid()), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-JST-FORBIDDEN");
    }

    [Fact]
    public async Task JobStatusHistory_OfOwnJob_ReturnsRows_AndUnknownJobIsEmpty()
    {
        var store = Substitute.For<IAuditReadStore>();
        var row = new JobStatusHistoryDto(Guid.NewGuid(), Me, "Draft", "Active", null, Ids.T0);
        store.GetJobStatusHistoryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new JobHistoryOwnerView(Me, new[] { row }), new JobHistoryOwnerView(null, Array.Empty<JobStatusHistoryDto>()));
        var handlers = Handlers(store, Users.Of(ActorType.Employer, Me));

        (await handlers.Handle(new GetJobStatusHistoryQuery(Guid.NewGuid()), default)).Value.Should().ContainSingle();
        (await handlers.Handle(new GetJobStatusHistoryQuery(Guid.NewGuid()), default)).Value.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-02")]
    public async Task CandidateInsight_AppliesCandidatePrivacyPolicy()
    {
        var store = Substitute.For<IAuditReadStore>();
        store.GetCandidateInsightAsync(default, default, default).ReturnsForAnyArgs(new CandidateInsightRaw(Me, "Immediate", 1500m, 88m, new[] { "expectedSalary" }, Ids.T0));
        var handlers = Handlers(store, Users.Of(ActorType.Employer, Me));

        var result = await handlers.Handle(new GetCandidateInsightQuery(Guid.NewGuid(), Guid.NewGuid()), default);

        result.Value.Availability.Should().Be("Immediate");
        result.Value.ExpectedSalary.Should().Be("unavailable");
        result.Value.Fit.Should().Be("88");
    }

    [Fact]
    public async Task CandidateInsight_OfAnotherEmployersPosting_IsForbidden_AndMissingIsNotFound()
    {
        var store = Substitute.For<IAuditReadStore>();
        store.GetCandidateInsightAsync(default, default, default).ReturnsForAnyArgs(new CandidateInsightRaw(Guid.NewGuid(), "x", 1m, 1m, Array.Empty<string>(), Ids.T0), (CandidateInsightRaw?)null);
        var handlers = Handlers(store, Users.Of(ActorType.Employer, Me));

        var act = () => handlers.Handle(new GetCandidateInsightQuery(Guid.NewGuid(), Guid.NewGuid()), default);
        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-AUDIT-INSIGHT-FORBIDDEN");

        var missing = await handlers.Handle(new GetCandidateInsightQuery(Guid.NewGuid(), Guid.NewGuid()), default);
        missing.Error!.Type.Should().Be(ErrorType.NotFound);
        missing.Error.Code.Should().Be("E-AUDIT-INSIGHT-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-07")]
    [Trait("AC", "AC-03")]
    public async Task EmployerDashboard_IsLookedUpByTheCallersOwnId_AndEmptyWhenNone()
    {
        var store = Substitute.For<IAuditReadStore>();
        store.GetEmployerDashboardAsync(default, default).ReturnsForAnyArgs((EmployerDashboardDto?)null);
        var handlers = Handlers(store, Users.Of(ActorType.Employer, Me));

        var result = await handlers.Handle(new GetEmployerDashboardQuery(), default);

        result.Value.Postings.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        await store.Received(1).GetEmployerDashboardAsync(Me, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationHistory_IsScopedToTheCaller_AndEmailLogIsAdminOnly()
    {
        var store = Substitute.For<IAuditReadStore>();
        store.ListNotificationLogAsync(default!, default, default!, default).ReturnsForAnyArgs(new PagedResult<NotificationLogDto>(Array.Empty<NotificationLogDto>(), 1, 20, 0));
        var jobSeeker = Handlers(store, Users.Of(ActorType.JobSeeker, Me));

        await jobSeeker.Handle(new ListNotificationHistoryQuery(), default);
        await store.Received(1).ListNotificationLogAsync(Arg.Any<string[]>(), Me, Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());

        var act = () => jobSeeker.Handle(new ListEmailLogQuery(), default);
        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-EMAILN-FORBIDDEN");
        var sms = () => jobSeeker.Handle(new ListSmsMessageLogQuery(), default);
        (await sms.Should().ThrowAsync<BusinessRuleViolationException>()).Which.ExternalCode.Should().Be("E-SMSN-FORBIDDEN");

        var admin = Handlers(store, Users.Of(ActorType.Administrator, Me));
        (await admin.Handle(new ListEmailLogQuery(), default)).IsSuccess.Should().BeTrue();
        await store.Received().ListNotificationLogAsync(Arg.Is<string[]>(c => c.SequenceEqual(new[] { "Email" })), null, Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
    }

    private static RequestHandlerSet Handlers(params object[] dependencies) => new(typeof(ApplicationAssembly).Assembly, dependencies);
}

public class ExportHandlerTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private readonly FakeStore _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));

    private RequestHandlerSet Handlers(Guid? user = null) => new(typeof(ApplicationAssembly).Assembly,
        _store, Users.Of(ActorType.Administrator, user ?? Admin), _clock);

    private Task<Result<ExportRequestResult>> Request(string type = "PostingsByRegion", string format = "Csv", Dictionary<string, string>? parameters = null, Guid? user = null) =>
        Handlers(user).Handle(
            new RequestAdministratorReportExportCommand(type, format, parameters ?? new Dictionary<string, string> { ["year"] = "2026" }), default);

    [Fact]
    [Trait("Story", "US-3.1.4-10")]
    [Trait("AC", "AC-01")]
    public async Task Request_StartsAQueuedJob()
    {
        var result = await Request();

        result.Value.Reused.Should().BeFalse();
        result.Value.Job.Status.Should().Be("Queued");
        _store.Jobs.Should().ContainSingle().Which.RequestedBy.Should().Be(Admin);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-10")]
    [Trait("AC", "AC-03")]
    public async Task Request_IdenticalWhileInProgress_ReusesTheJob()
    {
        var first = await Request();
        var second = await Request();

        second.Value.Reused.Should().BeTrue();
        second.Value.Job.Id.Should().Be(first.Value.Job.Id);
        _store.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task Request_DifferentParametersOrAdministrator_StartsAnotherJob()
    {
        await Request();
        await Request(parameters: new Dictionary<string, string> { ["year"] = "2025" });
        await Request(user: Guid.NewGuid());

        _store.Jobs.Should().HaveCount(3);
    }

    [Fact]
    public async Task Request_AfterTheFirstFinished_StartsANewJob()
    {
        var first = await Request();
        _store.Jobs.Single().StartGenerating();
        _store.Jobs.Single().Complete("ref", _clock.GetUtcNow().UtcDateTime);

        var second = await Request();

        second.Value.Reused.Should().BeFalse();
        second.Value.Job.Id.Should().NotBe(first.Value.Job.Id);
    }

    [Fact]
    public async Task Get_UnknownJob_IsNotFound()
    {
        var result = await Handlers().Handle(new GetExportJobQuery(Guid.NewGuid()), default);

        result.Error!.Code.Should().Be("E-AUDIT-EXPORT-NOT-FOUND");
    }

    [Fact]
    public async Task Runner_DrivesQueuedJobToReady_ThroughCommands()
    {
        await Request();
        var generator = Substitute.For<IReportGenerator>();
        generator.GenerateAsync(default, default, default!, default).ReturnsForAnyArgs(Result.Success("signed://report/1"));
        var runner = new ExportGenerationRunner(Sender(), _store, generator, NullLogger<ExportGenerationRunner>.Instance);

        (await runner.RunOnceAsync(5, default)).Should().Be(1);

        var job = _store.Jobs.Single();
        job.Status.Should().Be(ExportStatus.Ready);
        job.ResultRef.Should().Be("signed://report/1");
    }

    [Fact]
    public async Task Runner_WhenSourceFails_MarksTheJobFailedWithTheCode()
    {
        await Request();
        var generator = Substitute.For<IReportGenerator>();
        generator.GenerateAsync(default, default, default!, default).ReturnsForAnyArgs(Error.External("E-AUDIT-REPORT-SOURCE-UNAVAILABLE", "down"));
        var runner = new ExportGenerationRunner(Sender(), _store, generator, NullLogger<ExportGenerationRunner>.Instance);

        await runner.RunOnceAsync(5, default);

        _store.Jobs.Single().Status.Should().Be(ExportStatus.Failed);
        _store.Jobs.Single().FailureReason.Should().Be("E-AUDIT-REPORT-SOURCE-UNAVAILABLE");
    }

    [Fact]
    public async Task Runner_WhenTheSourceThrows_MarksFailedAndContinuesWithTheNextJob()
    {
        await Request();
        await Request(parameters: new Dictionary<string, string> { ["year"] = "2027" });
        var generator = Substitute.For<IReportGenerator>();
        generator.GenerateAsync(default, default, default!, default).ReturnsForAnyArgs<Task<Result<string>>>(
            _ => throw new HttpRequestException("boom"), _ => Task.FromResult(Result.Success("ok")));
        var runner = new ExportGenerationRunner(Sender(), _store, generator, NullLogger<ExportGenerationRunner>.Instance);

        (await runner.RunOnceAsync(5, default)).Should().Be(2);

        _store.Jobs.Select(j => j.Status).Should().BeEquivalentTo(new[] { ExportStatus.Failed, ExportStatus.Ready });
    }

    [Fact]
    [Trait("Story", "US-3.1.3-06")]
    [Trait("AC", "AC-04")]
    public async Task ArchiveHandler_ArchivesOnlyExpiredEntries_AndKeepsThem()
    {
        var old = AuditEntry.Record("bc", Guid.NewGuid(), AuditCategory.Access, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), null, null, "S", "1", OwnerScope.AdminOnly, "A",
            AuditOutcome.Success, null, null, new RetentionPolicy());
        var recent = AuditEntry.Record("bc", Guid.NewGuid(), AuditCategory.Access, _clock.GetUtcNow().UtcDateTime.AddMonths(-1), null, null, "S", "2", OwnerScope.AdminOnly, "A",
            AuditOutcome.Success, null, null, new RetentionPolicy());
        _store.Entries.AddRange(new[] { old, recent });
        var handler = Activator.CreateInstance(typeof(ApplicationAssembly).Assembly.GetTypes().Single(t => t.Name == "ArchiveHandler"), _store, new RetentionPolicy(), _clock)!;

        var result = await ((ICommandHandler<ArchiveExpiredAuditEntriesCommand, int>)handler).Handle(new ArchiveExpiredAuditEntriesCommand(100), default);

        result.Value.Should().Be(1);
        old.IsArchived.Should().BeTrue();
        recent.IsArchived.Should().BeFalse();
        _store.Entries.Should().HaveCount(2, "archived entries are kept, never purged");
    }

    /// <summary>A minimal in-process dispatcher for the three export commands (no pipeline: the unit of work is an infrastructure concern).</summary>
    private ISender Sender()
    {
        var handlers = Handlers();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<ExportJobSpec>>(), Arg.Any<CancellationToken>()).Returns(ci =>
            handlers.Handle((StartExportGenerationCommand)ci[0], default));
        sender.Send(Arg.Any<IRequest<Unit>>(), Arg.Any<CancellationToken>()).Returns(ci => ci[0] switch
        {
            CompleteExportJobCommand c => handlers.Handle(c, default),
            FailExportJobCommand f => handlers.Handle(f, default),
            _ => throw new NotSupportedException()
        });
        return sender;
    }
}

public class ValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ListQueries_PageSizeOutOfRange_IsInvalid(int pageSize)
    {
        new ListApiResponseLogQueryValidator().TestValidate(new ListApiResponseLogQuery(null, null, null, 1, pageSize)).ShouldHaveValidationErrorFor(x => x.PageSize).WithErrorCode("VAL.PageSize.OutOfRange");
    }

    [Fact]
    public void ListQueries_PageBelowOne_IsInvalid() =>
        new ListAccessLogQueryValidator().TestValidate(new ListAccessLogQuery(null, null, null, 0)).ShouldHaveValidationErrorFor(x => x.Page).WithErrorCode("VAL.Page.OutOfRange");

    [Fact]
    public void ListQueries_ToBeforeFrom_IsInvalid() =>
        new ListSubmissionLogQueryValidator().TestValidate(new ListSubmissionLogQuery(new DateTime(2026, 2, 1), new DateTime(2026, 1, 1), null)).ShouldHaveValidationErrorFor("To").WithErrorCode("VAL.To.BeforeFrom");

    [Theory]
    [InlineData("success")]
    [InlineData("FAILURE")]
    [InlineData("duplicate")]
    [InlineData(null)]
    public void ListQueries_KnownOutcome_IsValid(string? outcome) =>
        new ListSyncErrorLogQueryValidator().TestValidate(new ListSyncErrorLogQuery(null, null, outcome)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ListQueries_UnknownOutcome_IsInvalid() =>
        new ListApiResponseLogQueryValidator().TestValidate(new ListApiResponseLogQuery(null, null, "bogus")).ShouldHaveValidationErrorFor("Outcome").WithErrorCode("VAL.Outcome.Invalid");

    [Fact]
    public void JobAuditTrail_RequiresAPlatformJobId() =>
        new GetJobAuditTrailQueryValidator().TestValidate(new GetJobAuditTrailQuery("", null, null, null)).ShouldHaveValidationErrorFor(x => x.PlatformJobId);

    [Fact]
    public void UsageStatistics_RequiresBothDates()
    {
        var result = new GetIntegrationUsageStatisticsQueryValidator().TestValidate(new GetIntegrationUsageStatisticsQuery(default, default));
        result.ShouldHaveValidationErrorFor(x => x.From).WithErrorCode("VAL.From.Required");
        result.ShouldHaveValidationErrorFor(x => x.To).WithErrorCode("VAL.To.Required");
    }

    [Theory]
    [InlineData("Bogus", "Csv", "VAL.ReportType.Invalid")]
    [InlineData("", "Csv", "VAL.ReportType.Required")]
    [InlineData("PostingsBySector", "Pdf", "VAL.Format.Invalid")]
    [InlineData("PostingsBySector", "", "VAL.Format.Required")]
    public void ExportRequest_InvalidTypeOrFormat_IsRejected(string type, string format, string code) =>
        new RequestAdministratorReportExportValidator().TestValidate(new RequestAdministratorReportExportCommand(type, format, null)).Errors.Select(e => e.ErrorCode).Should().Contain(code);

    [Theory]
    [InlineData("PostingsBySector", "Csv")]
    [InlineData("userregistrations", "json")]
    [InlineData("TopSearches", "XML")]
    public void ExportRequest_ValidTypeAndFormat_IsAccepted(string type, string format) =>
        new RequestAdministratorReportExportValidator().TestValidate(new RequestAdministratorReportExportCommand(type, format, new Dictionary<string, string> { ["a"] = "b" })).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ExportRequest_TooManyParameters_IsRejected() =>
        new RequestAdministratorReportExportValidator().TestValidate(new RequestAdministratorReportExportCommand("PostingsBySector", "Csv",
            Enumerable.Range(0, 21).ToDictionary(i => $"k{i}", i => "v"))).Errors.Select(e => e.ErrorCode).Should().Contain("VAL.Parameters.Invalid");

    [Fact]
    public void GetExport_RequiresAnId() =>
        new GetExportJobQueryValidator().TestValidate(new GetExportJobQuery(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.Id);

    [Fact]
    public void JobStatusHistoryAndInsight_RequireIds()
    {
        new GetJobStatusHistoryQueryValidator().TestValidate(new GetJobStatusHistoryQuery(Guid.Empty)).ShouldHaveValidationErrorFor(x => x.JobPostingId);
        var insight = new GetCandidateInsightQueryValidator().TestValidate(new GetCandidateInsightQuery(Guid.Empty, Guid.Empty));
        insight.ShouldHaveValidationErrorFor(x => x.CandidateId);
        insight.ShouldHaveValidationErrorFor(x => x.JobPostingId);
    }

    [Fact]
    public void Paging_OnDashboardsAndNotificationQueries_IsValidated()
    {
        new GetSyncDashboardQueryValidator().TestValidate(new GetSyncDashboardQuery(1, 500)).ShouldHaveValidationErrorFor(x => x.PageSize);
        new ListNotificationHistoryQueryValidator().TestValidate(new ListNotificationHistoryQuery(0, 20)).ShouldHaveValidationErrorFor(x => x.Page);
        new ListEmailLogQueryValidator().TestValidate(new ListEmailLogQuery(1, 0)).ShouldHaveValidationErrorFor(x => x.PageSize);
        new ListSmsMessageLogQueryValidator().TestValidate(new ListSmsMessageLogQuery(1, 101)).ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}
