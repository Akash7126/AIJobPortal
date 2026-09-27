using System.Reflection;
using FluentValidation;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.AuditLogging.Application.Ingestion;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobPlatform.AuditLogging.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddAuditLoggingApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditIngestion>();
        services.AddScoped<ExportGenerationRunner>();
        services.AddSingleton(sp => new RetentionPolicy(sp.GetRequiredService<IOptions<AuditRetentionOptions>>().Value.Months));
        return services;
    }
}

public sealed class AuditRetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Live retention in months (default 12, A-02-012); older entries are archived, never purged.</summary>
    public int Months { get; set; } = RetentionPolicy.DefaultMonths;

    public int BatchSize { get; set; } = 500;

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
}

// ---------------------------------------------------------------------- request bases (role, forbidden code)

/// <summary>Base for requests only a given actor type may make.</summary>
public abstract record ActorRequest(ActorType Actor, string ForbiddenCode) : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { Actor };

    public string ForbiddenErrorCode => ForbiddenCode;
}

/// <summary>Base for administrator-only requests: Administrator actor type with MFA.</summary>
public abstract record AdminRequest(string ForbiddenCode) : ActorRequest(ActorType.Administrator, ForbiddenCode), IAuthorizedRequest
{
    bool IAuthorizedRequest.RequireMfa => true;
}

/// <summary>Any authenticated actor (own data only).</summary>
public abstract record AuthenticatedRequest(string ForbiddenCode) : IAuthorizedRequest
{
    public string ForbiddenErrorCode => ForbiddenCode;
}

// ---------------------------------------------------------------------- read models

public sealed record AuditEntryDto(
    Guid Id, string Category, DateTime OccurredAtUtc, Guid? ActorId, string? ActorType, string SubjectType, string SubjectId,
    string Action, string Outcome, string? Code, IReadOnlyDictionary<string, string> Details, bool IsArchived);

public sealed record EntryFilter(AuditCategory[] Categories, Guid? OwnerId = null, string? SubjectId = null, DateTime? FromUtc = null, DateTime? ToUtc = null,
    AuditOutcome? Outcome = null, bool IncludeArchived = false);

public sealed record SyncJobStatusDto(string PlatformJobId, string Status, string? ReasonCode, DateTime UpdatedAtUtc);

public sealed record SyncDashboardDto(int Pending, int Synced, int Failed, int Archived, PagedResult<SyncJobStatusDto> Jobs);

public sealed record IntegrationStatusDto(string Health, int Pending, int Synced, int Failed, int Archived, DateTime? LastSyncAtUtc, int SubmittedLast30Days);

public sealed record UsageDayDto(DateOnly Day, int Submitted, int Matched, int Viewed);

public sealed record UsageStatisticsDto(DateOnly From, DateOnly To, int Submitted, int Matched, int Viewed, IReadOnlyList<UsageDayDto> Days);

public sealed record JobStatusHistoryDto(Guid JobPostingId, Guid EmployerId, string? FromStatus, string ToStatus, string? Reason, DateTime ChangedAtUtc);

public sealed record NotificationLogDto(Guid NotificationId, string Channel, string Category, string MaskedRecipient, string? Subject, string Status, DateTime SentAtUtc);

public sealed record DashboardPostingDto(Guid JobPostingId, string Title, string Status, DateTime UpdatedAtUtc);

public sealed record EmployerDashboardDto(bool RegistrationApproved, int Postings, int ActivePostings, int Shortlists, IReadOnlyList<DashboardPostingDto> Items,
    DateTime? UpdatedAtUtc);

public sealed record CandidateInsightDto(Guid CandidateId, Guid JobPostingId, string Availability, string ExpectedSalary, string Fit, DateTime ComputedAtUtc);

/// <summary>Stored insight before the candidate-privacy policy is applied (the handler applies it).</summary>
public sealed record CandidateInsightRaw(Guid EmployerId, string? Availability, decimal? ExpectedSalary, decimal? FitScore, IReadOnlyCollection<string> Withheld,
    DateTime ComputedAtUtc);

public sealed record ExportJobDto(Guid Id, string ReportType, string Format, string Status, string? ResultRef, string? FailureReason, DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record JobHistoryOwnerView(Guid? EmployerId, IReadOnlyList<JobStatusHistoryDto> Rows);

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IAuditReadStore
{
    Task<PagedResult<AuditEntryDto>> ListEntriesAsync(EntryFilter filter, PageRequest page, CancellationToken ct = default);

    Task<SyncDashboardDto> GetSyncDashboardAsync(Guid partnerId, PageRequest page, CancellationToken ct = default);

    Task<IntegrationStatusDto> GetIntegrationStatusAsync(Guid partnerId, DateOnly today, CancellationToken ct = default);

    Task<UsageStatisticsDto> GetUsageAsync(Guid partnerId, UsageWindow window, CancellationToken ct = default);

    Task<JobHistoryOwnerView> GetJobStatusHistoryAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<PagedResult<NotificationLogDto>> ListNotificationLogAsync(string[] channels, Guid? recipientId, PageRequest page, CancellationToken ct = default);

    Task<EmployerDashboardDto?> GetEmployerDashboardAsync(Guid employerId, CancellationToken ct = default);

    Task<CandidateInsightRaw?> GetCandidateInsightAsync(Guid candidateProfileId, Guid jobPostingId, CancellationToken ct = default);
}

/// <summary>Anti-corruption port to the report source (BC-12, 3.1.4-10). The adapter is chosen by configuration (Reports:Provider = Simulated | Http).</summary>
public interface IReportGenerator
{
    /// <summary>Generates the report and returns a reference to the result (a signed link), or a failure.</summary>
    Task<JobPlatform.SharedKernel.Application.Results.Result<string>> GenerateAsync(ReportType type, ExportFormat format,
        IReadOnlyDictionary<string, string> parameters, CancellationToken ct = default);
}

// ---------------------------------------------------------------------- shared validation

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
}
