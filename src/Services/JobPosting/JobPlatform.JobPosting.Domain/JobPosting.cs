using JobPlatform.JobPosting.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.JobPosting.Domain;

/// <summary>The editable content of a posting, shared by <see cref="JobPosting.CreateDraft"/> and <see cref="JobPosting.Edit"/>.</summary>
public sealed record JobPostingFields(
    LocalizedText Title,
    LocalizedText Summary,
    IReadOnlyList<string> Skills,
    string CategoryCode,
    ContractType ContractType,
    EducationLevel? EducationLevel,
    IReadOnlyList<string> RequiredTraining,
    WorkFormat WorkFormat,
    JobLocation? Location,
    SalaryRange? Salary,
    int? MinExperienceYears,
    int? MaxExperienceYears,
    IReadOnlyList<string> RequiredLanguages,
    ApplicationDeadline Deadline,
    string? JobLink,
    IReadOnlyDictionary<string, string>? OtherFields);

/// <summary>
/// AGG-17 + AGG-21 (merged, decision D-01): a job posting and its lifecycle status share one identity and one state set.
/// Owner-only mutation, schema/taxonomy conformance is checked by the caller (<see cref="IJobPostingSchemaValidator"/>, an I/O port) before
/// <see cref="CreateDraft"/>/<see cref="Edit"/> are called; the aggregate itself only re-asserts the cheap, always-true invariants (INV-01/07/08).
/// </summary>
public sealed class JobPosting : AggregateRoot<Guid>
{
    private JobPosting()
    {
        Title = new LocalizedText(string.Empty, string.Empty);
        Summary = new LocalizedText(string.Empty, string.Empty);
        CategoryCode = string.Empty;
        ContentHash = string.Empty;
        Source = JobSource.FromEmployer();
        Visibility = JobVisibility.Public();
        Deadline = ApplicationDeadline.Create(DateTime.UtcNow, false);
        Skills = Array.Empty<string>();
        RequiredTraining = Array.Empty<string>();
        RequiredLanguages = Array.Empty<string>();
        OtherFields = new Dictionary<string, string>();
    }

    public Guid? EmployerAccountId { get; private set; }
    public JobSource Source { get; private set; }
    public LocalizedText Title { get; private set; }
    public LocalizedText Summary { get; private set; }
    public IReadOnlyList<string> Skills { get; private set; }
    public string CategoryCode { get; private set; }
    public ContractType ContractType { get; private set; }
    public EducationLevel? EducationLevelValue { get; private set; }
    public IReadOnlyList<string> RequiredTraining { get; private set; }
    public WorkFormat WorkFormat { get; private set; }
    public JobLocation? Location { get; private set; }
    public SalaryRange? Salary { get; private set; }
    public int? MinExperienceYears { get; private set; }
    public int? MaxExperienceYears { get; private set; }
    public IReadOnlyList<string> RequiredLanguages { get; private set; }
    public ApplicationDeadline Deadline { get; private set; }
    public string? JobLink { get; private set; }
    public IReadOnlyDictionary<string, string> OtherFields { get; private set; }
    public JobVisibility Visibility { get; private set; }
    public JobPostingStatus Status { get; private set; }
    public bool AdminSuspended { get; private set; }
    public string? SuspendReason { get; private set; }
    public int TaxonomyVersion { get; private set; }
    public string ContentHash { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Employer submits the posting form (US-3.2.1-01). Starts in <c>Draft</c>. Raises <c>JobPostingCreated</c> (handover section 3.1).</summary>
    public static JobPosting CreateDraft(Guid employerAccountId, JobPostingFields fields, Actor actor, int taxonomyVersion, string contentHash, DateTime nowUtc)
    {
        var posting = new JobPosting { Id = Guid.NewGuid(), EmployerAccountId = employerAccountId, Source = JobSource.FromEmployer(), CreatedAtUtc = nowUtc };
        posting.ApplyFields(fields, taxonomyVersion, contentHash);
        posting.Status = JobPostingStatus.Draft;
        posting.UpdatedAtUtc = nowUtc;
        posting.Raise(new JobPostingCreatedDomainEvent(nowUtc, posting.Id, employerAccountId, actor.Id, posting.Status, fields.Title.En, fields.CategoryCode,
            fields.Skills, posting.Visibility.Scope, posting.Source.Type, FormatLocation(fields.Location), fields.Salary?.Min, fields.Salary?.Max));
        return posting;
    }

    /// <summary>BC-02 import (Q-04, proposed): imported postings publish straight to <c>Active</c> since the external site already published them.</summary>
    public static JobPosting CreateFromExternal(JobSource source, JobPostingFields fields, int taxonomyVersion, string contentHash, DateTime nowUtc)
    {
        var posting = new JobPosting { Id = Guid.NewGuid(), EmployerAccountId = null, Source = source, CreatedAtUtc = nowUtc };
        posting.ApplyFields(fields, taxonomyVersion, contentHash);
        posting.Status = JobPostingStatus.Active;
        posting.PublishedAtUtc = nowUtc;
        posting.UpdatedAtUtc = nowUtc;
        var actorId = source.SourcePlatformId ?? Guid.Empty;
        posting.Raise(new JobPostingCreatedDomainEvent(nowUtc, posting.Id, Guid.Empty, actorId, posting.Status, fields.Title.En, fields.CategoryCode,
            fields.Skills, posting.Visibility.Scope, posting.Source.Type, FormatLocation(fields.Location), fields.Salary?.Min, fields.Salary?.Max));
        return posting;
    }

    /// <summary>US-3.2.1-03: owner edits the details (INV-04 <see cref="ErrorCodes.PostingForbidden"/>). A-01: allowed at any status except archived.</summary>
    public void Edit(JobPostingFields fields, Actor actor, int taxonomyVersion, string contentHash, DateTime nowUtc)
    {
        Check(Rules.PostingOwnerOnly(actor, EmployerAccountId ?? Guid.Empty, ErrorCodes.PostingForbidden));
        Check(NotArchived());

        var changed = new List<string>();
        void Track(string name, bool differs)
        {
            if (differs)
            {
                changed.Add(name);
            }
        }

        Track("title", !Equals(Title, fields.Title));
        Track("summary", !Equals(Summary, fields.Summary));
        Track("skills", !Skills.SequenceEqual(fields.Skills));
        Track("category", CategoryCode != fields.CategoryCode);
        Track("contractType", ContractType != fields.ContractType);
        Track("workFormat", WorkFormat != fields.WorkFormat);
        Track("educationLevel", EducationLevelValue != fields.EducationLevel);
        Track("location", !Equals(Location, fields.Location));
        Track("salary", !Equals(Salary, fields.Salary));
        Track("experience", MinExperienceYears != fields.MinExperienceYears || MaxExperienceYears != fields.MaxExperienceYears);
        Track("requiredLanguages", !RequiredLanguages.SequenceEqual(fields.RequiredLanguages));
        Track("requiredTraining", !RequiredTraining.SequenceEqual(fields.RequiredTraining));
        Track("deadline", !Equals(Deadline, fields.Deadline));
        Track("jobLink", JobLink != fields.JobLink);

        ApplyFields(fields, taxonomyVersion, contentHash);
        UpdatedAtUtc = nowUtc;
        if (changed.Count > 0)
        {
            Raise(new JobPostingUpdatedDomainEvent(nowUtc, Id, EmployerAccountId ?? Guid.Empty, Status, Status, actor.Id, changed));
        }
    }

    /// <summary>BC-02 import update (idempotent upsert; system actor bypasses the owner check).</summary>
    public void UpdateFromExternal(JobPostingFields fields, int taxonomyVersion, string contentHash, DateTime nowUtc) =>
        Edit(fields, Actor.System(Source.SourcePlatformId ?? Guid.Empty), taxonomyVersion, contentHash, nowUtc);

    /// <summary>US-3.2.1-03: visibility scope/target.</summary>
    public void SetVisibility(JobVisibility visibility, Actor actor, DateTime nowUtc)
    {
        Check(Rules.PostingOwnerOnly(actor, EmployerAccountId ?? Guid.Empty, ErrorCodes.PostingForbidden));
        Check(NotArchived());
        if (Equals(Visibility, visibility))
        {
            return;
        }

        Visibility = visibility;
        UpdatedAtUtc = nowUtc;
        Raise(new JobPostingUpdatedDomainEvent(nowUtc, Id, EmployerAccountId ?? Guid.Empty, Status, Status, actor.Id, new[] { "visibility" }));
    }

    /// <summary>US-3.2.1-03: extend the deadline without a full edit (must stay after "now").</summary>
    public void ExtendDeadline(ApplicationDeadline newDeadline, Actor actor, DateTime nowUtc)
    {
        Check(Rules.PostingOwnerOnly(actor, EmployerAccountId ?? Guid.Empty, ErrorCodes.PostingForbidden));
        Check(NotArchived());
        Rules.EnsureValid(newDeadline.IsPast(nowUtc), RuleCodes.PostingDeadlineInPast, "The new deadline must be in the future.", "deadline");

        Deadline = newDeadline;
        UpdatedAtUtc = nowUtc;
        Raise(new JobPostingUpdatedDomainEvent(nowUtc, Id, EmployerAccountId ?? Guid.Empty, Status, Status, actor.Id, new[] { "deadline" }));
    }

    /// <summary>draft -&gt; active. Requires the deadline to still be in the future (INV-08).</summary>
    public void Publish(Actor actor, DateTime nowUtc)
    {
        Check(Rules.PostingOwnerOnly(actor, EmployerAccountId ?? Guid.Empty, ErrorCodes.StatusForbidden));
        Rules.EnsureValid(Deadline.IsPast(nowUtc), RuleCodes.PostingDeadlineInPast, "Cannot publish with a deadline in the past.", "deadline");
        TransitionTo(JobPostingStatus.Active, actor, nowUtc, allowedFrom: JobPostingStatus.Draft);
        PublishedAtUtc ??= nowUtc;
    }

    /// <summary>active -&gt; paused.</summary>
    public void Pause(Actor actor, DateTime nowUtc) => TransitionTo(JobPostingStatus.Paused, actor, nowUtc, allowedFrom: JobPostingStatus.Active);

    /// <summary>paused -&gt; active. INV-06: refused while admin-suspended.</summary>
    public void Resume(Actor actor, DateTime nowUtc)
    {
        Check(Rules.NotAdminSuspended(AdminSuspended, actor.IsSystem));
        TransitionTo(JobPostingStatus.Active, actor, nowUtc, allowedFrom: JobPostingStatus.Paused);
    }

    /// <summary>active/paused -&gt; expired. Actor is the employer (manual) or System (auto-close job, US-3.2.1-01 AC-02).</summary>
    public void Expire(Actor actor, DateTime nowUtc) =>
        TransitionTo(JobPostingStatus.Expired, actor, nowUtc, JobPostingStatus.Active, JobPostingStatus.Paused);

    /// <summary>INV-07 RENEW_ONLY_WHEN_EXPIRED: expired -&gt; active with a new future deadline. INV-06 admin-suspended refuses renewal.</summary>
    public void Renew(ApplicationDeadline newDeadline, Actor actor, DateTime nowUtc)
    {
        Check(Rules.PostingOwnerOnly(actor, EmployerAccountId ?? Guid.Empty, ErrorCodes.PostingForbidden));
        Check(new BusinessRule(RuleCodes.PostingStateActive, "Only an expired posting can be renewed.", Status != JobPostingStatus.Expired,
            ErrorCodes.StateActive, BusinessRuleKind.Conflict));
        Check(Rules.NotAdminSuspended(AdminSuspended, actor.IsSystem));
        Rules.EnsureValid(newDeadline.IsPast(nowUtc), RuleCodes.PostingDeadlineInPast, "The new deadline must be in the future.", "deadline");

        var from = Status;
        Deadline = newDeadline;
        Status = JobPostingStatus.Active;
        UpdatedAtUtc = nowUtc;
        Raise(new JobPostingRenewedDomainEvent(nowUtc, Id, actor.Id, newDeadline.DateUtc));
        Raise(new JobPostingStatusUpdatedDomainEvent(nowUtc, Id, EmployerAccountId ?? Guid.Empty, from, Status, actor.Id, null));
    }

    /// <summary>INV-05 ARCHIVED_TERMINAL: any non-archived status -&gt; archived. Cannot be undone (must be recreated).</summary>
    public void Archive(Actor actor, DateTime nowUtc) => TransitionTo(JobPostingStatus.Archived, actor, nowUtc,
        JobPostingStatus.Draft, JobPostingStatus.Active, JobPostingStatus.Paused, JobPostingStatus.Expired);

    /// <summary>
    /// Generic dispatcher for <c>POST /jobs/{id}/status</c> (US-3.2.4-01): routes to the specific transition so every guard still applies.
    /// Renewal (expired -&gt; active) is not reachable here since it requires a new deadline; use <see cref="Renew"/>.
    /// </summary>
    public void ChangeStatus(JobPostingStatus to, Actor actor, DateTime nowUtc)
    {
        switch (to)
        {
            case JobPostingStatus.Active when Status == JobPostingStatus.Draft:
                Publish(actor, nowUtc);
                return;
            case JobPostingStatus.Active when Status == JobPostingStatus.Paused:
                Resume(actor, nowUtc);
                return;
            case JobPostingStatus.Paused when Status == JobPostingStatus.Active:
                Pause(actor, nowUtc);
                return;
            case JobPostingStatus.Expired:
                Expire(actor, nowUtc);
                return;
            case JobPostingStatus.Archived:
                Archive(actor, nowUtc);
                return;
            default:
                Check(Rules.PostingOwnerOnly(actor, EmployerAccountId ?? Guid.Empty, ErrorCodes.StatusForbidden));
                Check(NotArchived());
                throw new BusinessRuleViolationException(RuleCodes.PostingInvalidTransition,
                    $"Cannot move a posting from {Status} to {to}.", ErrorCodes.InvalidTransition, BusinessRuleKind.Conflict,
                    new Dictionary<string, object?> { ["from"] = Status.ToString(), ["to"] = to.ToString() });
        }
    }

    /// <summary>Consumed event handler (BC-08 <c>JobOfferingSuspended</c>): forces <c>Paused</c> + the suspended flag. Idempotent.</summary>
    public void ApplyAdminSuspension(string reason, DateTime nowUtc)
    {
        if (AdminSuspended)
        {
            return;
        }

        var from = Status;
        AdminSuspended = true;
        SuspendReason = reason;
        if (Status is JobPostingStatus.Active or JobPostingStatus.Draft)
        {
            Status = JobPostingStatus.Paused;
        }

        UpdatedAtUtc = nowUtc;
        Raise(new JobPostingStatusUpdatedDomainEvent(nowUtc, Id, EmployerAccountId ?? Guid.Empty, from, Status, Guid.Empty, reason));
    }

    /// <summary>Refreshes the taxonomy version an in-flight validation was captured against; does not itself change the schema.</summary>
    public void StampTaxonomyVersion(int version) => TaxonomyVersion = version;

    private void TransitionTo(JobPostingStatus to, Actor actor, DateTime nowUtc, params JobPostingStatus[] allowedFrom)
    {
        Check(Rules.PostingOwnerOnly(actor, EmployerAccountId ?? Guid.Empty, ErrorCodes.StatusForbidden));
        Check(new BusinessRule(RuleCodes.PostingStateArchived, "An archived posting cannot change state; recreate it instead.",
            Status == JobPostingStatus.Archived, ErrorCodes.StateArchived, BusinessRuleKind.Conflict));
        if (!allowedFrom.Contains(Status))
        {
            throw new BusinessRuleViolationException(RuleCodes.PostingInvalidTransition, $"Cannot move a posting from {Status} to {to}.",
                ErrorCodes.InvalidTransition, BusinessRuleKind.Conflict, new Dictionary<string, object?> { ["from"] = Status.ToString(), ["to"] = to.ToString() });
        }

        var from = Status;
        Status = to;
        UpdatedAtUtc = nowUtc;
        Raise(new JobPostingStatusUpdatedDomainEvent(nowUtc, Id, EmployerAccountId ?? Guid.Empty, from, to, actor.Id, null));
    }

    private IBusinessRule NotArchived() =>
        new BusinessRule(RuleCodes.PostingStateArchived, "An archived posting cannot be edited; recreate it instead.",
            Status == JobPostingStatus.Archived, ErrorCodes.StateArchived, BusinessRuleKind.Conflict);

    private void ApplyFields(JobPostingFields fields, int taxonomyVersion, string contentHash)
    {
        Rules.EnsureValid(string.IsNullOrWhiteSpace(fields.Title.En) && string.IsNullOrWhiteSpace(fields.Title.Ar), RuleCodes.PostingRequiredField,
            "Title is required.", "title", ErrorCodes.RequiredField);
        Rules.EnsureValid(string.IsNullOrWhiteSpace(fields.Summary.En) && string.IsNullOrWhiteSpace(fields.Summary.Ar), RuleCodes.PostingRequiredField,
            "Summary is required.", "summary", ErrorCodes.RequiredField);
        Rules.EnsureValid(fields.Skills.Count == 0, RuleCodes.PostingRequiredField, "At least one skill is required.", "skills", ErrorCodes.RequiredField);

        Title = fields.Title;
        Summary = fields.Summary;
        Skills = fields.Skills.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        CategoryCode = fields.CategoryCode;
        ContractType = fields.ContractType;
        EducationLevelValue = fields.EducationLevel;
        RequiredTraining = fields.RequiredTraining.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        WorkFormat = fields.WorkFormat;
        Location = fields.Location;
        Salary = fields.Salary;
        MinExperienceYears = fields.MinExperienceYears;
        MaxExperienceYears = fields.MaxExperienceYears;
        RequiredLanguages = fields.RequiredLanguages.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Deadline = fields.Deadline;
        JobLink = fields.JobLink;
        OtherFields = fields.OtherFields is null ? new Dictionary<string, string>() : new Dictionary<string, string>(fields.OtherFields);
        TaxonomyVersion = taxonomyVersion;
        ContentHash = contentHash;
    }

    private static string? FormatLocation(JobLocation? location) =>
        location is null ? null : string.Join(", ", new[] { location.City, location.Governorate }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
