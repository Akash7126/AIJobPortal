using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

/// <summary>
/// AGG (proposed) JobPreference (handover section 3.5): 1:1 with a profile. Deliberately *not* RowVersion-enforced - concurrent writes are
/// last-write-wins (AC-03), unlike Profile's reject-on-conflict. No domain events are published for it.
/// </summary>
public sealed class JobPreference : Entity<Guid>
{
    private readonly List<string> _jobTypes = new();
    private readonly List<string> _industries = new();
    private readonly List<string> _locations = new();
    private readonly List<WorkArrangement> _workArrangements = new();

    private JobPreference()
    {
    }

    public Guid ProfileId { get; private set; }
    public IReadOnlyList<string> JobTypes => _jobTypes;
    public IReadOnlyList<string> Industries => _industries;
    public IReadOnlyList<string> Locations => _locations;
    public SalaryRange? SalaryExpectation { get; private set; }
    public IReadOnlyList<WorkArrangement> WorkArrangements => _workArrangements;
    public DateTime UpdatedAtUtc { get; private set; }

    public static JobPreference CreateEmpty(Guid profileId) => new() { Id = profileId, ProfileId = profileId };

    public void EnsureOwnedBy(Actor actor, Guid ownerAccountId) => Check(Rules.NotOwner(actor, ownerAccountId));

    public void Set(IReadOnlyList<string> jobTypes, IReadOnlyList<string> industries, IReadOnlyList<string> locations, SalaryRange? salary,
        IReadOnlyList<WorkArrangement> workArrangements, DateTime nowUtc)
    {
        _jobTypes.Clear();
        _jobTypes.AddRange(jobTypes);
        _industries.Clear();
        _industries.AddRange(industries);
        _locations.Clear();
        _locations.AddRange(locations);
        SalaryExpectation = salary;
        _workArrangements.Clear();
        _workArrangements.AddRange(workArrangements);
        UpdatedAtUtc = nowUtc;
    }
}
