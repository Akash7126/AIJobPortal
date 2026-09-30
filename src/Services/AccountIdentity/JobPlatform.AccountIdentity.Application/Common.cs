using System.Reflection;
using JobPlatform.AccountIdentity.Application.Services.Accounts;
using JobPlatform.AccountIdentity.Application.Services.Consent;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.AccountIdentity.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    /// <summary>Registers domain services and helpers that are not handlers. Handlers/validators are registered with AddRequestHandlersFrom.</summary>
    public static IServiceCollection AddAccountIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<AccountRegistrar>();
        services.AddScoped<Security.SessionIssuer>();
        services.AddScoped<Services.Accounts.RegistrationWorkflow>();
        services.AddScoped<IAccessAuthorizer, Security.AccessAuthorizer>();
        services.AddSingleton<Events.AccountIdentityEventMapper>();
        services.AddSingleton<SharedKernel.Messaging.Interfaces.IDomainEventMapper>(sp => sp.GetRequiredService<Events.AccountIdentityEventMapper>());
services.AddScoped<AdminAccountService>();
        services.AddScoped<ConsentService>();
                return services;
    }
}

/// <summary>Base for administrator-only requests: Administrator actor type, a permission, MFA satisfied.</summary>
public abstract record AdminAuthorized(string RequiredPermissionCode, string ForbiddenCode = ErrorCodes.AuthForbidden) : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public string? RequiredPermission => RequiredPermissionCode;

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => ForbiddenCode;
}

/// <summary>Base for requests any authenticated user may call.</summary>
public abstract record AuthenticatedRequest : IAuthorizedRequest;

/// <summary>Base for service-to-service (/internal/v1) requests: client-credentials token with the internal scope.</summary>
public abstract record ServiceAuthorized : IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

/// <summary>Masks PII for admin read models (THR-038).</summary>
public static class Masking
{
    public static string Mobile(string value) => value.Length <= 6 ? "****" : string.Concat(value.AsSpan(0, 4), "****", value.AsSpan(value.Length - 3));

    public static string? Email(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var at = value.IndexOf('@');
        return at <= 1 ? "***" + value[at..] : string.Concat(value.AsSpan(0, 1), "***", value.AsSpan(at));
    }
}

public static class AuthenticationStatus
{
    public const string Authenticated = "Authenticated";
    public const string MfaRequired = "MfaRequired";
    public const string MfaEnrollmentRequired = "MfaEnrollmentRequired";
    public const string EmailCodeSent = "EmailCodeSent";
}

/// <summary>Translates domain login outcomes to the published errors. The invalid-credentials error is identical for unknown user and wrong password.</summary>
internal static class LoginErrors
{
    public static Error From(LoginAttemptResult result, DateTime nowUtc) => result.Outcome switch
    {
        LoginOutcome.Locked => Error.TooManyRequests(result.ExternalCode!, "The account is temporarily locked after too many failed attempts.",
            result.LockedUntilUtc is { } until ? until - nowUtc : TimeSpan.FromMinutes(15)) with { RuleCode = result.RuleCode },
        LoginOutcome.NotActive => Error.Forbidden(result.ExternalCode!, "The account cannot sign in in its current state.") with { RuleCode = result.RuleCode },
        _ => InvalidCredentials
    };

    public static Error InvalidCredentials { get; } =
        Error.Unauthorized(ErrorCodes.AuthInvalidCredentials, "The credentials are invalid.");
}

internal static class AccountErrors
{
    public static Error NotFound { get; } = Error.NotFound(ErrorCodes.NotFound, "The account was not found.");
}

internal static class ConcurrencyErrors
{
    /// <summary>Returned when If-Match is present and no longer matches the aggregate's current version (HTTP 412).</summary>
    public static Error PreconditionFailed { get; } = Error.PreconditionFailed("E-PRECONDITION-FAILED",
        "The resource was modified since it was read. Reload it and retry with its current ETag.");
}
