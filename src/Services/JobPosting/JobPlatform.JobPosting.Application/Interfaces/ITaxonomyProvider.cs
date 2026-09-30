using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.Interfaces;

/// <summary>Port to BC-08's taxonomy (skills/jobs/trainings), cached in Redis (handover section 9). Adapter chosen by Taxonomy:Provider.</summary>
public interface ITaxonomyProvider
{
    /// <summary>The current version and valid codes for a taxonomy type ("skills", "jobs" categories, "trainings"). Throws
    /// <see cref="TaxonomyUnavailableException"/> after its retry budget (30 s / 3 retries, handover section 3.5).</summary>
    Task<TaxonomySnapshot> GetAsync(string type, CancellationToken ct = default);
}
