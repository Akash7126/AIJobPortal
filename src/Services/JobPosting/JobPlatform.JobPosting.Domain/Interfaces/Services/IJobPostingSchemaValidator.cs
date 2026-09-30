namespace JobPlatform.JobPosting.Domain.Interfaces.Services;

/// <summary>
/// Domain service port (implementation is an I/O adapter in Infrastructure): checks categories/skills/qualifications against the platform
/// taxonomy version in effect at submission (INV-02) and the Schema.org JobPosting shape. Throws <see cref="TaxonomyUnavailableException"/>
/// after its own retry budget (handover section 3.5: 30 s / 3 retries) so the caller can map it to <see cref="ErrorCodes.UpstreamTimeout"/>.
/// </summary>
public interface IJobPostingSchemaValidator
{
    Task<SchemaValidationResult> ValidateAsync(JobPostingFields fields, CancellationToken ct = default);
}
