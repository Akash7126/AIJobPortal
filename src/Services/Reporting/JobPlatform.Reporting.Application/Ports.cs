namespace JobPlatform.Reporting.Application;

public sealed record EventRow(string SourceBc, string EventType, string ActivityType, string ActorType, string ActorKey, DateTime OccurredAtUtc);

public sealed record PostingRow(Guid Id, string Status, string? Category, string? Location, decimal? SalaryMin, decimal? SalaryMax, string Source, string? Title, DateTime FirstSeenAtUtc,
    DateTime? ClosedAtUtc);

public sealed record SkillRow(string Skill, string Side, DateTime OccurredAtUtc);

public sealed record RegistrationRow(string Milestone, string ActorType, string? Governorate, DateTime OccurredAtUtc);

public sealed record MatchRow(string Kind, decimal? Score, int? ItemCount, DateTime OccurredAtUtc);

public sealed record MetricRow(string Metric, decimal Value, DateTime SampledAtUtc);

public sealed record NotificationRow(string Channel, string Category, string Status, DateTime OccurredAtUtc);

public sealed record OutcomeRow(Guid JobPostingId, int Shortlisted, int FollowUps, decimal FitSum, int FitCount, DateTime FirstShortlistedAtUtc);

public sealed record DailyCount(DateOnly Day, string Metric, long Count);

/// <summary>What a Power BI publish returns.</summary>
public sealed record PowerBiPublication(string DatasetId, string ReportUrl);

public sealed record GeneratedFile(string FileName, string ContentType, byte[] Content);
