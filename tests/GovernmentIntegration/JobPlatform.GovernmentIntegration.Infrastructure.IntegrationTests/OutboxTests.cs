using System.Text.Json.Nodes;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.IntegrationTests;

public class OutboxTests
{
    [Fact]
    [Trait("Story", "US-3.1.2-03")]
    [Trait("AC", "AC-04")]
    public async Task MarkMatched_WritesTheOutboxRowInTheSameSaveAsTheAggregate()
    {
        await using var database = Db.New();
        var employerId = Guid.NewGuid();
        await using var db = database.NewContext();
        var verification = EmployerVerification.Request(Guid.NewGuid(), employerId, new Submission("REG-1", "VAT-1", "+970591234567"));
        db.EmployerVerifications.Add(verification);
        await db.SaveChangesAsync();

        verification.MarkMatched(SourceSystem.MoL, Db.T0);
        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync();
        (row.Type, row.Exchange, row.RoutingKey, row.Status).Should().Be(
            ("EmployerVerificationApproved", "jobplatform.government-integration.events", "employer-verification.approved.v1", OutboxStatus.Pending));
        var payload = JsonNode.Parse(row.Payload)!;
        payload["employerAccountId"]!.GetValue<Guid>().Should().Be(employerId);
        payload["method"]!.GetValue<string>().Should().Be("Automatic");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-01")]
    public async Task RecordMatch_WritesGovernmentVerificationDataImportedOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        var data = GovernmentVerificationData.Request(Guid.NewGuid(), new Subject(SubjectType.Employer, Guid.NewGuid()), SourceSystem.MoL,
            AccessPurpose.EmployerVerification, Db.T0);
        db.GovernmentVerificationData.Add(data);
        await db.SaveChangesAsync();

        data.RecordMatch(new[] { new VerifiedField("k", "v") }, Db.T0);
        await db.SaveChangesAsync();

        var row = await db.Set<OutboxMessage>().SingleAsync();
        row.Type.Should().Be("GovernmentVerificationDataImported");
        JsonNode.Parse(row.Payload)!["outcome"]!.GetValue<string>().Should().Be("Verified");
    }

    [Fact]
    public async Task RolledBackTransaction_LeavesNeitherTheAggregateNorTheOutboxRow()
    {
        await using var database = Db.New();
        await using var db = database.NewContext();
        await db.BeginTransactionAsync();
        db.EmployerVerifications.Add(EmployerVerification.Request(Guid.NewGuid(), Guid.NewGuid(), new Submission("R", "V", "+970591234567")));
        await db.SaveChangesAsync();
        await db.RollbackTransactionAsync();

        (await db.EmployerVerifications.CountAsync()).Should().Be(0);
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }
}
