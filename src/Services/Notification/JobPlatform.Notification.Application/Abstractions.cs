using System.Reflection;
using JobPlatform.Notification.Application.Composition;
using JobPlatform.Notification.Application.Delivery;
using JobPlatform.Notification.Application.Events;
using JobPlatform.Notification.Application.Services.Admin;
using JobPlatform.Notification.Application.Services.Delivery;
using JobPlatform.Notification.Application.Services.InApp;
using JobPlatform.Notification.Application.Services.Preferences;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.Notification.Application;

public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        services.AddScoped<NotificationComposer>();
        services.AddScoped<OutboundDispatcher>();
        services.AddSingleton<NotificationEventMapper>();
        services.AddSingleton<IDomainEventMapper>(sp => sp.GetRequiredService<NotificationEventMapper>());
services.AddScoped<AdminService>();
        services.AddScoped<DeliveryService>();
        services.AddScoped<InAppService>();
        services.AddScoped<PreferenceService>();
                return services;
    }
}

/// <summary>Delivery and compliance settings (section "Notification").</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notification";

    public string DefaultLocale { get; set; } = "en";

    /// <summary>Public base URL used in unsubscribe links.</summary>
    public string PublicBaseUrl { get; set; } = "https://jobplatform.local";

    public string EmailSenderAddress { get; set; } = "no-reply@jobplatform.local";

    /// <summary>Anti-spam control: only a verified sender domain (SPF/DKIM/DMARC) may send e-mail (3.6.1-05).</summary>
    public bool SenderDomainVerified { get; set; } = true;

    /// <summary>Telecom control: approved alphanumeric sender ids (3.6.3-06).</summary>
    public string[] ApprovedSmsSenderIds { get; set; } = { "JobPlatform" };

    public string SmsSenderId { get; set; } = "JobPlatform";
    public int DispatchBatchSize { get; set; } = 20;
    public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromSeconds(1);
    public bool DispatcherEnabled { get; set; } = true;
    public bool DigestEnabled { get; set; } = true;
    public TimeSpan DigestInterval { get; set; } = TimeSpan.FromHours(24);
}

// ---------------------------------------------------------------------- request bases

public abstract record AuthenticatedRequest(string ForbiddenCode) : IAuthorizedRequest
{
    public string ForbiddenErrorCode => ForbiddenCode;
}

public abstract record AdminRequest(string ForbiddenCode) : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public string ForbiddenErrorCode => ForbiddenCode;

    bool IAuthorizedRequest.RequireMfa => true;
}

/// <summary>Service-to-service (/internal/v1) request: client-credentials token.</summary>
public abstract record ServiceRequest : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

// ---------------------------------------------------------------------- ports (anti-corruption layer)

public sealed record RecipientContact(string? Email, string? Mobile, string Locale);

public sealed record EmailEnvelope(string From, string To, string Subject, string Body, string? UnsubscribeUrl);

public sealed record SmsEnvelope(string SenderId, string To, string Body);

/// <summary>A provider answer. Failure is a normal outcome; a timeout is reported as <see cref="TimedOut"/>.</summary>
public sealed record ProviderResult(bool Accepted, string? ProviderMessageId, bool TimedOut = false, string? Error = null)
{
    public static ProviderResult Ok(string id) => new(true, id);
    public static ProviderResult Timeout() => new(false, null, true);
    public static ProviderResult Fail(string error) => new(false, null, false, error);
}

public static class Masking
{
    public static string Mobile(string value) => value.Length <= 6 ? "****" : string.Concat(value.AsSpan(0, 4), "****", value.AsSpan(value.Length - 3));

    public static string Email(string value)
    {
        var at = value.IndexOf('@');
        return at <= 1 ? "***" + (at < 0 ? string.Empty : value[at..]) : string.Concat(value.AsSpan(0, 1), "***", value.AsSpan(at));
    }
}
