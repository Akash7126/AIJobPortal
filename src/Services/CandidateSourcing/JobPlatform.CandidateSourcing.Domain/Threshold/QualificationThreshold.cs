using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.CandidateSourcing.Domain.Threshold;

/// <summary>
/// Proposed aggregate (story US-3.3.3-03): an employer-set 0-100% qualification cutoff for one job posting.
/// INV-06 (snapshot-at-run-start) is enforced by the application: a recommendation run reads <see cref="Percent"/> once at the start
/// of the computation, so a concurrent <see cref="Set"/> only ever affects the next run.
/// </summary>
public sealed class QualificationThreshold : AggregateRoot<Guid>
{
    private QualificationThreshold()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public Guid JobPostingId { get; private set; }

    public int Percent { get; private set; }

    public int ThresholdVersion { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static QualificationThreshold Open(Guid employerAccountId, Guid jobPostingId, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(), EmployerAccountId = employerAccountId, JobPostingId = jobPostingId, Percent = 0, ThresholdVersion = 0, UpdatedAtUtc = nowUtc
    };

    /// <summary>INV-04: 0-100. INV-05: owner-only.</summary>
    public void Set(int percent, Guid callerAccountId, DateTime nowUtc)
    {
        Check(Rules.OwnerOnly(EmployerAccountId, callerAccountId));
        Rules.EnsureValid(percent is < 0 or > 100, RuleCodes.ThresholdOutOfRange, "The qualification threshold must be between 0 and 100.", "percent");
        Percent = percent;
        ThresholdVersion++;
        UpdatedAtUtc = nowUtc;
    }
}
