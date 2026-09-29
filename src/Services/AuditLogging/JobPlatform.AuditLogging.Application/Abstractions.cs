using System.Reflection;
using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.AuditLogging.Application.Ingestion;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
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
services.AddScoped<AuditLogQueryService>();
        services.AddScoped<NotificationLogQueryService>();
        services.AddScoped<PartnerDashboardService>();
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

public sealed record EntryFilter(AuditCategory[] Categories, Guid? OwnerId = null, string? SubjectId = null, DateTime? FromUtc = null, DateTime? ToUtc = null,
    AuditOutcome? Outcome = null, bool IncludeArchived = false);

/// <summary>Stored insight before the candidate-privacy policy is applied (the handler applies it).</summary>
public sealed record CandidateInsightRaw(Guid EmployerId, string? Availability, decimal? ExpectedSalary, decimal? FitScore, IReadOnlyCollection<string> Withheld,
    DateTime ComputedAtUtc);

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
