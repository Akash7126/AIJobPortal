using System.Reflection;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.EmployerOnboarding.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddEmployerOnboardingApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.EmployerOnboardingEventMapper>();
        services.AddSingleton<SharedKernel.Messaging.IDomainEventMapper>(sp => sp.GetRequiredService<Events.EmployerOnboardingEventMapper>());
        return services;
    }
}

/// <summary>Employer self-service command/query: Employer actor type, no MFA requirement.</summary>
public abstract record EmployerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.MediaForbidden;
}

public abstract record EmployerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.MediaForbidden;
}

/// <summary>Administrator command/query: Administrator actor type with MFA, refused with E-AUM-FORBIDDEN.</summary>
public abstract record AdminCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.RegistrationForbidden;
}

public abstract record AdminQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Administrator };

    public bool RequireMfa => true;

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.RegistrationForbidden;
}

/// <summary>Service-to-service query (/internal/v1): client-credentials token (actor type System).</summary>
public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

public static class ActorFactory
{
    public static Actor From(SharedKernel.Application.Ports.ICurrentUser user) => new(user.UserId ?? Guid.Empty, user.ActorType == ActorType.Administrator);
}

// ---------------------------------------------------------------------- read models

public sealed record CompanyIdentityView(string Name, string CompanyId, string RegistrationNumber);

public sealed record Level2View(string Website, string Industry, string Size, string Governorate, string City, string? Street, string Description);

public sealed record EmployerRegistrationView(
    Guid EmployerRegistrationId, Guid EmployerAccountId, string Status, CompanyIdentityView? Identity, Level2View? Level2, DateTime OpenedAtUtc,
    Guid? ApprovedBy, DateTime? ApprovedAtUtc, byte[] RowVersion);

public sealed record CompanyMediaView(Guid CompanyMediaId, string Kind, string FileName, string ContentType, long SizeBytes, bool IsPrimaryLogo, DateTime UploadedAtUtc);

public sealed record EmployerStandingView(Guid EmployerAccountId, bool Approved, bool Verified, string? Badge);

public sealed record CompanyPublicInfoView(Guid EmployerAccountId, string Name, string? LogoUrl, string Industry, string Size, string Website);

// ---------------------------------------------------------------------- ports

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IEmployerReadStore
{
    Task<EmployerRegistrationView?> GetRegistrationAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<PagedResult<EmployerRegistrationView>> ListRegistrationsAsync(string? status, PageRequest page, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyMediaView>> ListMediaAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<CompanyPublicInfoView?> GetCompanyPublicInfoAsync(Guid employerAccountId, CancellationToken ct = default);
}

/// <summary>Stores an uploaded file's bytes and returns its storage key; content is addressed by key, never re-derived. A local-disk adapter is the
/// default; a real deployment swaps in object storage behind this same port.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default);

    /// <summary>Public (or short-lived signed) URL a browser can fetch the file from.</summary>
    string UrlFor(string storageKey);
}

public sealed record ScanResult(bool Clean, string? Reason);

/// <summary>Anti-corruption port to a malware scanner. The default adapter never quarantines (no scanner is wired in this environment).</summary>
public interface IMalwareScanner
{
    Task<ScanResult> ScanAsync(Stream content, string contentType, CancellationToken ct = default);
}

/// <summary>Cache-aside port (foundation section 10); implemented in Infrastructure over ICacheStore. Failures degrade to the database.</summary>
public interface IEmployerCache
{
    Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default);

    Task SetStandingAsync(Guid employerAccountId, EmployerStandingView view, CancellationToken ct = default);

    Task<CompanyPublicInfoView?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default);

    Task SetCompanyAsync(Guid employerAccountId, CompanyPublicInfoView view, CancellationToken ct = default);

    Task InvalidateAsync(Guid employerAccountId, CancellationToken ct = default);
}
