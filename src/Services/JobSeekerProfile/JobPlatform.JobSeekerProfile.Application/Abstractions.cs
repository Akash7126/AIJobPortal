using System.Reflection;
using JobPlatform.JobSeekerProfile.Domain;
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

// ---------------------------------------------------------------------- read models

public sealed record EducationView(Guid Id, string Degree, string Institution, DateTime? From, DateTime? To, string Source);
public sealed record ExperienceView(Guid Id, string Company, string Role, DateTime? From, DateTime? To, string Source);
public sealed record SkillView(Guid Id, string Name, string Kind, string Class, string Source);
public sealed record TrainingView(Guid Id, string Name, string? Provider, DateTime? CompletedOn);
public sealed record CertificateView(Guid Id, string Name, string? Issuer, DateTime? IssuedOn);
public sealed record SalaryRangeView(decimal? Min, decimal? Max, string? Currency);
public sealed record AddressView(string? Governorate, string? City, string? Street);
public sealed record SocialLinkView(string Network, string Url);

public sealed record ProfileView(
    Guid ProfileId, Guid OwnerAccountId, string Status, string FullName, string Email, string MobileNumber, string Gender,
    IReadOnlyList<EducationView> Education, IReadOnlyList<ExperienceView> Experience, IReadOnlyList<SkillView> Skills,
    IReadOnlyList<TrainingView> Training, IReadOnlyList<CertificateView> Certificates, SalaryRangeView? SalaryExpectation, AddressView? Address,
    decimal? YearsOfExperience, IReadOnlyList<SocialLinkView> SocialLinks, string? Statement, string? Bio, int CompletionPercent, byte[] RowVersion);

public sealed record ProfileCompletionView(int Percent, IReadOnlyList<string> MissingSections);

public sealed record SharedProfileView(
    string FullName, IReadOnlyList<SkillView> Skills, IReadOnlyList<EducationView> Education, IReadOnlyList<ExperienceView> Experience, string? Statement,
    string? Bio);

public sealed record JobPreferenceView(
    Guid ProfileId, IReadOnlyList<string> JobTypes, IReadOnlyList<string> Industries, IReadOnlyList<string> Locations, SalaryRangeView? SalaryExpectation,
    IReadOnlyList<string> WorkArrangements, DateTime UpdatedAtUtc);

public sealed record PrivacySettingView(Guid ProfileId, string Visibility, bool PublicSharingActive, string DeletionState, DateTime? DeactivationRequestedAtUtc);

public sealed record ShareLinkView(Guid ShareLinkId, Guid ProfileId, string Token, string Url, bool IsActive, DateTime CreatedAtUtc);

public sealed record DocumentView(Guid DocumentId, string OwnerType, Guid OwnerId, string FileName, long SizeBytes, string ContentType, string DocumentType,
    DateTime UploadedAtUtc);

public sealed record ResumeView(Guid ResumeId, Guid ProfileId, string FileName, long SizeBytes, string Format, DateTime UploadedAtUtc);

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
