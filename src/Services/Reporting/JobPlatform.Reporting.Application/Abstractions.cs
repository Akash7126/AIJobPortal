using System.Reflection;
using JobPlatform.Reporting.Application.Ingestion;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobPlatform.Reporting.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddReportingApplication(this IServiceCollection services)
    {
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ReportingOptions>>().Value.ToPolicy());
        services.AddScoped<EventIngestionService>();
        services.AddScoped<IReportAccessGuard, ReportAccessGuard>();
        services.AddScoped<ReportEngine>();
        services.AddScoped<ReportRunner>();
        services.AddScoped<LaborMarketReportBuilder>();
        services.AddScoped<ExportGenerationRunner>();
        services.AddScoped<ScheduleRunner>();
        services.AddScoped<AlertEvaluator>();
        services.AddScoped<MetricsSampler>();
        services.AddSingleton<IReportFileGenerator, ReportFileGenerator>();
        services.AddSingleton<ReportViewBuilder>();
        services.AddSingleton<IReportLinkSigner, HmacReportLinkSigner>();
        services.AddScoped<ReportingOptionsAccessor>();
        services.AddScoped<ExportContentBuilder>();
        services.AddSingleton<JobPlatform.SharedKernel.Messaging.IDomainEventMapper, ReportingEventMapper>();
        services.AddScoped<PerformanceReader>();

        foreach (var type in Assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IFactProjector<>)))
            {
                services.AddScoped(iface, type);
            }
        }

        return services;
    }
}

/// <summary>Reporting settings (section "Reporting").</summary>
public sealed class ReportingOptions
{
    public const string SectionName = "Reporting";

    /// <summary>Below this many records an aggregate is flagged insufficient instead of shown (handover 3.9).</summary>
    public int MinSampleSize { get; set; } = StatisticsPolicy.DefaultMinSampleSize;

    /// <summary>Small-cell suppression threshold (Q-07).</summary>
    public int MinCell { get; set; } = StatisticsPolicy.DefaultMinCell;

    /// <summary>Legal minimum retention of activity facts in months (Q-02: open, legal input needed).</summary>
    public int LegalMinimumRetentionMonths { get; set; } = 6;

    /// <summary>Salt of the pseudonymous actor keys. Must come from a secret store in production.</summary>
    public string ActorKeySalt { get; set; } = "dev-actor-key-salt";

    /// <summary>HMAC key of signed report links. Must come from a secret store in production.</summary>
    public string LinkSigningKey { get; set; } = "dev-link-signing-key-change-me-0123456789";

    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Public base URL used inside signed links.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5112";

    public int MaxReportRows { get; set; } = 100_000;

    /// <summary>Power BI publish: per-attempt timeout and attempts before falling back to the built-in view (handover 6.2: 30 s x 3).</summary>
    public TimeSpan PowerBiTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int PowerBiAttempts { get; set; } = 3;

    public StatisticsPolicy ToPolicy() => new(MinSampleSize, MinCell);
}

// ---------------------------------------------------------------------- request bases

/// <summary>Base of every administrator request of a report category: Administrator actor type with MFA; refusals carry the module's own error code.</summary>
public abstract record CategoryRequest(ReportCategory Category) : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => CategoryCodes.ForbiddenFor(Category);
}

public static class CategoryCodes
{
    public static string ForbiddenFor(ReportCategory category) => category switch
    {
        ReportCategory.ActivityLogs => ReportingErrorCodes.ActivityForbidden,
        ReportCategory.EmploymentStatistics => ReportingErrorCodes.EmploymentForbidden,
        ReportCategory.SystemPerformance => ReportingErrorCodes.PerformanceForbidden,
        _ => ReportingErrorCodes.CustomForbidden
    };
}

public abstract record ActivityRequest() : CategoryRequest(ReportCategory.ActivityLogs);

public abstract record EmploymentRequest() : CategoryRequest(ReportCategory.EmploymentStatistics);

public abstract record PerformanceRequest() : CategoryRequest(ReportCategory.SystemPerformance);

public abstract record CustomRequest() : CategoryRequest(ReportCategory.Custom);

/// <summary>Commands that record an access decision must commit it even when the request is refused (the denial is the audit trail).</summary>
public abstract record CustomCommandRequest() : CategoryRequest(ReportCategory.Custom), IPersistOnFailure;

public abstract record ActivityCommandRequest() : CategoryRequest(ReportCategory.ActivityLogs), IPersistOnFailure;

public abstract record PerformanceCommandRequest() : CategoryRequest(ReportCategory.SystemPerformance), IPersistOnFailure;

public abstract record EmploymentCommandRequest() : CategoryRequest(ReportCategory.EmploymentStatistics), IPersistOnFailure;

/// <summary>Access-rule administration is always administrator-only (never governed by a rule, so nobody can lock themselves out).</summary>
public abstract record AccessAdminRequest() : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => ReportingErrorCodes.CustomForbidden;
}
