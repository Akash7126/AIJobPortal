using System.Reflection;
using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.JobSeekerProfile.Application.DTOs.Resume;
using JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.JobSeekerProfile.Application;

/// <summary>Anchor for assembly scanning (handlers, validators) from the composition root.</summary>
public static class ApplicationAssembly
{
    public static Assembly Assembly => typeof(ApplicationAssembly).Assembly;

    public static IServiceCollection AddJobSeekerProfileApplication(this IServiceCollection services)
    {
        services.AddSingleton<Events.JobSeekerProfileEventMapper>();
        services.AddSingleton<IDomainEventMapper>(sp => sp.GetRequiredService<Events.JobSeekerProfileEventMapper>());
        return services;
    }
}

/// <summary>Job-seeker-owner command: JobSeeker actor type only, refused with E-JSRPM-FORBIDDEN.</summary>
public abstract record JobSeekerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };
    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

public abstract record JobSeekerQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };
    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

/// <summary>Employer-owner command (US-3.1.2-08 company documents): Employer actor type, refused with E-ERPM-FORBIDDEN.</summary>
public abstract record EmployerCommand<TResponse> : ICommand<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };
    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.CompanyForbidden;
}

/// <summary>Service-to-service query (/internal/v1): client-credentials token (actor type System).</summary>
public abstract record ServiceQuery<TResponse> : IQuery<TResponse>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System };
}

// ---------------------------------------------------------------------- ports

/// <summary>Anti-corruption port to durable file storage (resumes/documents). Adapter chosen by FileStorage:Provider.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(string ownerFolder, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Short-lived signed URL another service (BC-10) can fetch the content from.</summary>
    Task<(string Url, DateTime ExpiresAtUtc)> GetSignedUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct = default);
}

public interface IMalwareScanner
{
    Task<bool> IsCleanAsync(Stream content, CancellationToken ct = default);
}

/// <summary>Renders a share link as a scannable image. See docs/bc-status/BC-04.md for the current limitation.</summary>
public interface IQrCodeRenderer
{
    string RenderSvg(string url);
}

/// <summary>Anti-corruption port to BC-03 (Q-03, foundation 9.5): requests account deactivation/deletion.</summary>
public interface IAccountIdentityClient
{
    Task<bool> RequestDeactivationAsync(Guid accountId, string reason, string standing, CancellationToken ct = default);
}

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IProfileReadStore
{
    Task<ProfileView?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);
    Task<ProfileView?> GetByIdAsync(Guid profileId, CancellationToken ct = default);
    Task<SharedProfileView?> GetSharedAsync(Guid profileId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentView>> ListDocumentsAsync(Domain.Common.DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task<ResumeView?> GetCurrentResumeAsync(Guid profileId, CancellationToken ct = default);
}

public static class CacheKeys
{
    public static readonly TimeSpan MatchingViewTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PrivacyTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan SharedProfileTtl = TimeSpan.FromMinutes(2);

    public static string Matching(Guid profileId) => $"matching:{profileId}";
    public static string Privacy(Guid profileId) => $"privacy:{profileId}";
    public static string Shared(string token) => $"share:{token}";
    public static string Reference(string type) => $"reference:{type}";
}
