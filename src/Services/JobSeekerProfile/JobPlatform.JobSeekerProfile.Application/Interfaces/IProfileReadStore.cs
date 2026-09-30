using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.JobSeekerProfile.Application.DTOs.Resume;
using JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;

namespace JobPlatform.JobSeekerProfile.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IProfileReadStore
{
    Task<ProfileView?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);
    Task<ProfileView?> GetByIdAsync(Guid profileId, CancellationToken ct = default);
    Task<SharedProfileView?> GetSharedAsync(Guid profileId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentView>> ListDocumentsAsync(Domain.Common.DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task<ResumeView?> GetCurrentResumeAsync(Guid profileId, CancellationToken ct = default);
}
