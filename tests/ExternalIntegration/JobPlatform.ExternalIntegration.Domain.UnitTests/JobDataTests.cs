using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain.UnitTests;

public class JobDataTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static JobData Received(Guid? sourcePlatformId = null, string sourceJobId = "job-1") =>
        JobData.Receive(Guid.NewGuid(), Guid.NewGuid(), sourcePlatformId ?? Guid.NewGuid(), sourceJobId, "{}", JobDataModel.Push, At);

    private static StandardJob ValidJob(string? sourceUrl = "https://partner.example/jobs/1") =>
        new("Backend Engineer", "Build things.", new[] { "C#", "SQL" }, "FullTime", "Remote", At.AddMonths(1), "Ramallah", sourceUrl);

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    public void Receive_WithoutSourcePlatformId_ThrowsSourceIdentityRequired()
    {
        var act = () => JobData.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "job-1", "{}", JobDataModel.Push, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.JobDataSourceIdentityRequired);
    }

    [Fact]
    public void Receive_StartsInReceivedStatus()
    {
        var jobData = Received();

        jobData.Status.Should().Be(JobDataStatus.Received);
        jobData.PlatformJobId.Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-05")]
    public void Accept_WithRequiredFields_AssignsPlatformJobIdAndRaisesEvent()
    {
        var jobData = Received();
        jobData.Standardize(ValidJob());

        jobData.Accept(Guid.NewGuid(), "Public", At);

        jobData.Status.Should().Be(JobDataStatus.Accepted);
        jobData.PlatformJobId.Should().NotBeNullOrEmpty();
        jobData.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobDataImportedDomainEvent>()
            .Which.IsUpdate.Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.4.1-02")]
    [Trait("AC", "AC-04")]
    public void Accept_SecondTime_ReusesPlatformJobIdAndMarksUpdate()
    {
        var jobData = Received();
        jobData.Standardize(ValidJob());
        jobData.Accept(Guid.NewGuid(), "Public", At);
        var firstId = jobData.PlatformJobId;
        jobData.ClearDomainEvents();

        jobData.Standardize(ValidJob());
        jobData.Accept(Guid.NewGuid(), "Public", At.AddDays(1));

        jobData.PlatformJobId.Should().Be(firstId);
        jobData.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobDataImportedDomainEvent>().Which.IsUpdate.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-03")]
    public void Accept_WithoutStandardization_ThrowsRequiredFieldMissing()
    {
        var jobData = Received();

        var act = () => jobData.Accept(Guid.NewGuid(), "Public", At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.JobDataRequiredFieldMissing);
        ex.ExternalCode.Should().Be(ErrorCodes.JobDataInvalidField);
    }

    [Fact]
    public void Accept_WithNoSkills_ThrowsRequiredFieldMissing()
    {
        var jobData = Received();
        jobData.Standardize(new StandardJob("Title", "Summary", Array.Empty<string>(), "FullTime", "Remote", null, "Ramallah", null));

        var act = () => jobData.Accept(Guid.NewGuid(), "Public", At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.JobDataRequiredFieldMissing);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-04")]
    public void UpdateRaw_ResetsStatusToReceived_ForTheUpsertPath()
    {
        var jobData = Received();
        jobData.Standardize(ValidJob());
        jobData.Accept(Guid.NewGuid(), "Public", At);

        jobData.UpdateRaw("{\"title\":\"new\"}", null, At.AddDays(1));

        jobData.Status.Should().Be(JobDataStatus.Received);
        jobData.RawPayload.Should().Contain("new");
    }

    [Fact]
    public void Reject_SetsRejectedStatus()
    {
        var jobData = Received();

        jobData.Reject();

        jobData.Status.Should().Be(JobDataStatus.Rejected);
    }
}
