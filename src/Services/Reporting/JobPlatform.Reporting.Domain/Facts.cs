using System.Security.Cryptography;
using System.Text;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>Activity types the user-activity read model reports (US-3.5.1-01 AC-02). Derived from the consumed event, never from personal data.</summary>
public static class ActivityTypes
{
    public const string Registration = "Registration";
    public const string Approval = "Approval";
    public const string Profile = "Profile";
    public const string Resume = "Resume";
    public const string JobPosting = "JobPosting";
    public const string Interaction = "Interaction";
    public const string CandidateSearch = "CandidateSearch";
    public const string Matching = "Matching";
    public const string Notification = "Notification";
    public const string Integration = "Integration";
    public const string Verification = "Verification";
    public const string Content = "Content";
    public const string Administration = "Administration";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Registration, Approval, Profile, Resume, JobPosting, Interaction, CandidateSearch, Matching, Notification, Integration, Verification, Content, Administration
    };
}

/// <summary>Pseudonymous actor keys: analytics never stores an account id (THR-046). SHA-256 over a deployment salt, truncated.</summary>
public static class ActorKeys
{
    public static string Pseudonymise(Guid? actorId, string salt) =>
        actorId is null || actorId == Guid.Empty
            ? "anonymous"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + actorId.Value.ToString("N"))))[..16].ToLowerInvariant();
}

/// <summary>FactEvent: one row per consumed event (generic ingestion, K-3). MessageId is unique so a redelivery never counts twice.</summary>
public sealed class FactEvent : Entity<Guid>
{
    private FactEvent()
    {
    }

    public Guid MessageId { get; private set; }
    public string SourceBc { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string ActivityType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public string ActorKey { get; private set; } = string.Empty;
    public string? SubjectId { get; private set; }

    public static FactEvent Record(Guid messageId, string sourceBc, string eventType, string activityType, DateTime occurredAtUtc, string actorType, string actorKey, string? subjectId)
    {
        Guard.Ensure(messageId != Guid.Empty && !string.IsNullOrWhiteSpace(sourceBc) && !string.IsNullOrWhiteSpace(eventType), ReportingRuleCodes.InvalidFact,
            "A fact needs a message id, a source BC and an event type.");
        return new FactEvent
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            SourceBc = sourceBc,
            EventType = eventType,
            ActivityType = activityType,
            OccurredAtUtc = occurredAtUtc,
            ActorType = actorType,
            ActorKey = actorKey,
            SubjectId = subjectId
        };
    }
}

/// <summary>FactJobPosting: current state of one posting (BC-09 postings and BC-02 imports). Created/Updated/StatusUpdated may arrive in any order.</summary>
public sealed class FactJobPosting : Entity<Guid>
{
    private FactJobPosting()
    {
    }

    public string Status { get; private set; } = "unknown";
    public string? Category { get; private set; }
    public string? Location { get; private set; }
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }
    public string Source { get; private set; } = "Employer";
    public string? Title { get; private set; }
    public bool HasDetails { get; private set; }
    public DateTime FirstSeenAtUtc { get; private set; }
    public DateTime StatusChangedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public long LastVersion { get; private set; }

    public static bool IsTerminal(string status) => status.ToLowerInvariant() is "expired" or "archived" or "closed" or "deactivated" or "deleted";

    public static FactJobPosting Open(Guid jobPostingId, DateTime atUtc) => new() { Id = jobPostingId, FirstSeenAtUtc = atUtc, StatusChangedAtUtc = atUtc };

    /// <summary>Full details (creation or import). Applied once; later events only move the status.</summary>
    public void ApplyDetails(string status, string? title, string? category, string? location, decimal? salaryMin, decimal? salaryMax, string source, DateTime atUtc, long version)
    {
        if (!HasDetails)
        {
            Title = title;
            Category = category;
            Location = location;
            SalaryMin = salaryMin;
            SalaryMax = salaryMax;
            Source = source;
            FirstSeenAtUtc = atUtc < FirstSeenAtUtc ? atUtc : FirstSeenAtUtc;
            HasDetails = true;
        }

        ApplyStatus(status, atUtc, version);
    }

    /// <summary>Out-of-order safe: an event older than the last applied version is ignored.</summary>
    public void ApplyStatus(string status, DateTime atUtc, long version)
    {
        if (version < LastVersion)
        {
            return;
        }

        LastVersion = version;
        if (!string.Equals(Status, status, StringComparison.OrdinalIgnoreCase))
        {
            Status = status;
            StatusChangedAtUtc = atUtc;
        }

        ClosedAtUtc = IsTerminal(status) ? ClosedAtUtc ?? atUtc : null;
    }

    /// <summary>
    /// An admin suspension (BC-08's JobOfferingSuspended) always wins, regardless of ordering: its event carries BC-08's own
    /// aggregate version, not this posting's, so it is not comparable to <see cref="LastVersion"/> and must not be gated by it.
    /// </summary>
    public void ApplyAdministrativeSuspension(DateTime atUtc)
    {
        if (!string.Equals(Status, "paused", StringComparison.OrdinalIgnoreCase))
        {
            Status = "paused";
            StatusChangedAtUtc = atUtc;
        }
    }
}

/// <summary>FactSkillDemand: a skill mentioned by a posting (demand) or a parsed resume (supply). Unique per subject, skill and side.</summary>
public sealed class FactSkillDemand : Entity<Guid>
{
    public const string Demand = "Demand";
    public const string Supply = "Supply";

    private FactSkillDemand()
    {
    }

    public string Skill { get; private set; } = string.Empty;
    public string Side { get; private set; } = Demand;
    public Guid SubjectId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static FactSkillDemand Of(string skill, string side, Guid subjectId, DateTime atUtc) =>
        new() { Id = Guid.NewGuid(), Skill = skill.Trim().ToLowerInvariant(), Side = side, SubjectId = subjectId, OccurredAtUtc = atUtc };
}

/// <summary>FactMatch: one match computation (score) or recommendation set.</summary>
public sealed class FactMatch : Entity<Guid>
{
    private FactMatch()
    {
    }

    public string Kind { get; private set; } = "Score";
    public Guid? JobPostingId { get; private set; }
    public decimal? Score { get; private set; }
    public string? ConfigVersion { get; private set; }
    public int? ItemCount { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static FactMatch ForScore(Guid matchScoreId, Guid jobPostingId, decimal score, string configVersion, DateTime atUtc) =>
        new() { Id = matchScoreId, Kind = "Score", JobPostingId = jobPostingId, Score = score, ConfigVersion = configVersion, OccurredAtUtc = atUtc };

    public static FactMatch Recommendation(Guid recommendationId, int count, DateTime atUtc) =>
        new() { Id = recommendationId, Kind = "Recommendation", ItemCount = count, OccurredAtUtc = atUtc };
}

/// <summary>FactRegistration: an account/profile milestone.</summary>
public sealed class FactRegistration : Entity<Guid>
{
    private FactRegistration()
    {
    }

    public Guid MessageId { get; private set; }
    public string Milestone { get; private set; } = string.Empty;
    public string ActorType { get; private set; } = string.Empty;
    public string? Governorate { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static FactRegistration Of(Guid messageId, string milestone, string actorType, string? governorate, DateTime atUtc) =>
        new() { Id = Guid.NewGuid(), MessageId = messageId, Milestone = milestone, ActorType = actorType, Governorate = governorate, OccurredAtUtc = atUtc };
}

/// <summary>FactNotification: one delivery (status follows NotificationStatusUpdated).</summary>
public sealed class FactNotification : Entity<Guid>
{
    private FactNotification()
    {
    }

    public string Channel { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }

    public static FactNotification Of(Guid notificationId, string channel, string category, string status, DateTime atUtc) =>
        new() { Id = notificationId, Channel = channel, Category = category, Status = status, OccurredAtUtc = atUtc };

    public void UpdateStatus(string status) => Status = status;
}

/// <summary>FactSystemMetric: one metric sample from the telemetry source (Q-04).</summary>
public sealed class FactSystemMetric : Entity<Guid>
{
    private FactSystemMetric()
    {
    }

    public string Metric { get; private set; } = string.Empty;
    public decimal Value { get; private set; }
    public DateTime SampledAtUtc { get; private set; }
    public string Source { get; private set; } = string.Empty;

    public static FactSystemMetric Sample(string metric, decimal value, DateTime atUtc, string source)
    {
        Guard.Ensure(PerformanceMetrics.IsKnown(metric), ReportingRuleCodes.InvalidFact, $"Unknown metric '{metric}'.");
        return new FactSystemMetric { Id = Guid.NewGuid(), Metric = metric, Value = value, SampledAtUtc = atUtc, Source = source };
    }
}

/// <summary>FactOutcome: shortlisting outcome per posting. Only postings with follow-up data (a computed insight) are reported (RP.Outcome.EXCLUDE_NO_FOLLOWUP).</summary>
public sealed class FactOutcome : Entity<Guid>
{
    private FactOutcome()
    {
    }

    public int Shortlisted { get; private set; }
    public int FollowUps { get; private set; }
    public decimal FitSum { get; private set; }
    public int FitCount { get; private set; }
    public DateTime FirstShortlistedAtUtc { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }

    public bool HasFollowUp => FollowUps > 0;

    public static FactOutcome ForPosting(Guid jobPostingId, DateTime atUtc) => new() { Id = jobPostingId, FirstShortlistedAtUtc = atUtc, LastUpdatedAtUtc = atUtc };

    public void Shortlist(DateTime atUtc)
    {
        Shortlisted++;
        FirstShortlistedAtUtc = atUtc < FirstShortlistedAtUtc ? atUtc : FirstShortlistedAtUtc;
        LastUpdatedAtUtc = atUtc;
    }

    public void FollowUp(decimal? fit, DateTime atUtc)
    {
        FollowUps++;
        if (fit is { } f)
        {
            FitSum += f;
            FitCount++;
        }

        LastUpdatedAtUtc = atUtc;
    }
}

/// <summary>AggDaily: pre-aggregated daily counter (handover 3.10). Metric is "event.&lt;EventType&gt;" or "activity.&lt;Type&gt;"; rebuilt from FactEvent on demand.</summary>
public sealed class AggDaily : Entity<Guid>
{
    private AggDaily()
    {
    }

    public DateOnly Day { get; private set; }
    public string Metric { get; private set; } = string.Empty;
    public long Count { get; private set; }

    public static AggDaily For(DateOnly day, string metric, long count = 0) => new() { Id = Guid.NewGuid(), Day = day, Metric = metric, Count = count };

    public void Add(long by) => Count += by;
}
