using JobPlatform.Reporting.Application.Interfaces;
using JobPlatform.Reporting.Application.Interfaces.Ingestion;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;

namespace JobPlatform.Reporting.Application.Ingestion;

// Projectors for FactJobPosting and FactSkillDemand (employment statistics, industry, geography, salary, skill trends).
// Every projector tolerates reordering (a status before its creation opens a placeholder row) and redelivery.

internal static class SkillFacts
{
    public static async Task AddAsync(IFactStore facts, IEnumerable<string> skills, string side, Guid subjectId, DateTime atUtc, CancellationToken ct)
    {
        foreach (var skill in skills.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToLowerInvariant()).Distinct())
        {
            if (!await facts.SkillFactExistsAsync(skill, side, subjectId, ct))
            {
                facts.Add(FactSkillDemand.Of(skill, side, subjectId, atUtc));
            }
        }
    }
}

public sealed class JobPostingCreatedProjector(IFactStore facts) : IFactProjector<JobPostingCreatedIntegrationEvent>
{
    public async Task ProjectAsync(JobPostingCreatedIntegrationEvent e, CancellationToken ct)
    {
        var posting = await facts.GetOrOpenPostingAsync(e.JobPostingId, e.OccurredOnUtc, ct);
        posting.ApplyDetails(e.Status, e.Title, e.Category, e.Location, e.SalaryMin, e.SalaryMax, e.Source, e.OccurredOnUtc, e.AggregateVersion);
        await SkillFacts.AddAsync(facts, e.Skills, FactSkillDemand.Demand, e.JobPostingId, e.OccurredOnUtc, ct);
    }
}

public sealed class JobPostingUpdatedProjector(IFactStore facts) : IFactProjector<JobPostingUpdatedIntegrationEvent>
{
    public async Task ProjectAsync(JobPostingUpdatedIntegrationEvent e, CancellationToken ct) =>
        (await facts.GetOrOpenPostingAsync(e.JobPostingId, e.OccurredOnUtc, ct)).ApplyStatus(e.ToStatus, e.OccurredOnUtc, e.AggregateVersion);
}

public sealed class JobPostingStatusUpdatedProjector(IFactStore facts) : IFactProjector<JobPostingStatusUpdatedIntegrationEvent>
{
    public async Task ProjectAsync(JobPostingStatusUpdatedIntegrationEvent e, CancellationToken ct) =>
        (await facts.GetOrOpenPostingAsync(e.JobPostingId, e.OccurredOnUtc, ct)).ApplyStatus(e.ToStatus, e.OccurredOnUtc, e.AggregateVersion);
}

/// <summary>An administrator suspension closes the posting for statistics purposes (it is no longer open demand).</summary>
public sealed class JobOfferingSuspendedProjector(IFactStore facts) : IFactProjector<JobOfferingSuspendedIntegrationEvent>
{
    public async Task ProjectAsync(JobOfferingSuspendedIntegrationEvent e, CancellationToken ct) =>
        (await facts.GetOrOpenPostingAsync(e.JobPostingId, e.OccurredOnUtc, ct)).ApplyAdministrativeSuspension(e.OccurredOnUtc);
}

/// <summary>External imports are demand too. The posting id of an import is BC-02's job data id (BC-09 uses the platform job id, which is not in the event).</summary>
public sealed class JobDataImportedProjector(IFactStore facts) : IFactProjector<JobDataImportedIntegrationEvent>
{
    public async Task ProjectAsync(JobDataImportedIntegrationEvent e, CancellationToken ct)
    {
        var posting = await facts.GetOrOpenPostingAsync(e.JobDataId, e.OccurredOnUtc, ct);
        posting.ApplyDetails("active", e.Title, null, e.Location, null, null, "External", e.OccurredOnUtc, e.AggregateVersion);
        await SkillFacts.AddAsync(facts, e.Skills, FactSkillDemand.Demand, e.JobDataId, e.OccurredOnUtc, ct);
    }
}

public sealed class JobPostAttributionUpdatedProjector(IFactStore facts) : IFactProjector<JobPostAttributionUpdatedIntegrationEvent>
{
    public async Task ProjectAsync(JobPostAttributionUpdatedIntegrationEvent e, CancellationToken ct)
    {
        if (e.ToStatus.Equals("Updated", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        (await facts.GetOrOpenPostingAsync(e.JobPostAttributionId, e.OccurredOnUtc, ct)).ApplyStatus(e.ToStatus, e.OccurredOnUtc, e.AggregateVersion);
    }
}

/// <summary>Supply side of skills: what parsed resumes offer.</summary>
public sealed class ResumeParsedSkillsProjector(IFactStore facts) : IFactProjector<ResumeParsedDataComputedIntegrationEvent>
{
    public Task ProjectAsync(ResumeParsedDataComputedIntegrationEvent e, CancellationToken ct) =>
        SkillFacts.AddAsync(facts, e.Skills, FactSkillDemand.Supply, e.ResumeId, e.OccurredOnUtc, ct);
}

public sealed class ParsedProfileSkillsProjector(IFactStore facts) : IFactProjector<ParsedProfileDataUpdatedIntegrationEvent>
{
    public Task ProjectAsync(ParsedProfileDataUpdatedIntegrationEvent e, CancellationToken ct) =>
        SkillFacts.AddAsync(facts, e.Skills, FactSkillDemand.Supply, e.ResumeId, e.OccurredOnUtc, ct);
}
