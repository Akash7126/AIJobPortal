using FluentValidation.TestHelper;

namespace JobPlatform.ExternalIntegration.Application.UnitTests;

public class ValidatorTests
{
    private readonly PushJobDataValidator _push = new();
    private readonly ConfigureSyncScheduleValidator _schedule = new();
    private readonly ConfigureJobDataMappingValidator _mapping = new();
    private readonly SyncJobPostAttributionValidator _attribution = new();
    private readonly DeprecateApiVersionValidator _deprecate = new();
    private readonly RegisterSoftwareInterfaceValidator _softwareInterface = new();
    private readonly RegisterExternalJobSiteValidator _register = new();

    private static PushJobDataCommand ValidPush() =>
        new("src-1", "Backend Engineer", "Build things.", new[] { "C#" }, "FullTime", "Remote", DateTime.UtcNow.AddMonths(1), "Ramallah",
            "https://partner.example/1", null);

    [Fact]
    public void PushJobData_Valid_HasNoErrors() => _push.TestValidate(ValidPush()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void PushJobData_MissingTitle_HasError() => _push.TestValidate(ValidPush() with { Title = "" }).ShouldHaveValidationErrorFor(c => c.Title);

    [Fact]
    public void PushJobData_EmptySkills_HasError() =>
        _push.TestValidate(ValidPush() with { Skills = Array.Empty<string>() }).ShouldHaveValidationErrorFor(c => c.Skills);

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("http://partner.example/1")]
    public void PushJobData_InvalidSourceUrl_HasError(string url) =>
        _push.TestValidate(ValidPush() with { SourceUrl = url }).ShouldHaveValidationErrorFor(c => c.SourceUrl);

    [Fact]
    public void ConfigureSyncSchedule_OnDemand_HasNoErrors() =>
        _schedule.TestValidate(new ConfigureSyncScheduleCommand("OnDemand", null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ConfigureSyncSchedule_ScheduledWithoutCron_HasError() =>
        _schedule.TestValidate(new ConfigureSyncScheduleCommand("Scheduled", null)).ShouldHaveValidationErrorFor(c => c.Cron);

    [Fact]
    public void ConfigureSyncSchedule_ScheduledWithEveryMinuteCron_HasError() =>
        _schedule.TestValidate(new ConfigureSyncScheduleCommand("Scheduled", "* * * * *")).ShouldHaveValidationErrorFor(c => c.Cron);

    [Fact]
    public void ConfigureSyncSchedule_ScheduledWithFiveMinuteCron_HasNoErrors() =>
        _schedule.TestValidate(new ConfigureSyncScheduleCommand("Scheduled", "*/5 * * * *")).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void ConfigureJobDataMapping_DuplicateTargetField_HasError() =>
        _mapping.TestValidate(new ConfigureJobDataMappingCommand(new[]
            {
                new MappingRuleInput("a", "title", "None"), new MappingRuleInput("b", "title", "None")
            }, "v1"))
            .ShouldHaveValidationErrorFor(c => c.Rules);

    [Fact]
    public void ConfigureJobDataMapping_InvalidTransform_HasError() =>
        _mapping.TestValidate(new ConfigureJobDataMappingCommand(new[] { new MappingRuleInput("a", "title", "Nope") }, "v1"))
            .ShouldHaveValidationErrorFor("Rules[0].Transform");

    [Fact]
    public void SyncJobPostAttribution_ExtendDeadlineWithoutDeadline_HasError() =>
        _attribution.TestValidate(new SyncJobPostAttributionCommand("plat-1", "ExtendDeadline", null, null)).ShouldHaveValidationErrorFor(c => c.Deadline);

    [Fact]
    public void SyncJobPostAttribution_Close_HasNoErrors() =>
        _attribution.TestValidate(new SyncJobPostAttributionCommand("plat-1", "Close", null, null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void DeprecateApiVersion_SunsetTooSoon_HasError() =>
        _deprecate.TestValidate(new DeprecateApiVersionCommand("v1", DateTime.UtcNow.AddDays(1))).ShouldHaveValidationErrorFor(c => c.SunsetAtUtc);

    [Fact]
    public void DeprecateApiVersion_BadVersionFormat_HasError() =>
        _deprecate.TestValidate(new DeprecateApiVersionCommand("version-1", DateTime.UtcNow.AddDays(120))).ShouldHaveValidationErrorFor(c => c.Version);

    [Fact]
    public void RegisterSoftwareInterface_NonHttpsEndpoint_HasError() =>
        _softwareInterface.TestValidate(new RegisterSoftwareInterfaceCommand("ExternalJobSite", "X", "http://x.example"))
            .ShouldHaveValidationErrorFor(c => c.Endpoint);

    [Fact]
    public void RegisterExternalJobSite_NonHttpsBaseUrl_HasError() =>
        _register.TestValidate(new RegisterExternalJobSiteCommand("X", "http://x.example", false)).ShouldHaveValidationErrorFor(c => c.BaseUrl);
}
