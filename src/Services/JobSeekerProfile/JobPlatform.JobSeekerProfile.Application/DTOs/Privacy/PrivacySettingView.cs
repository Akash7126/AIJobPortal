namespace JobPlatform.JobSeekerProfile.Application.DTOs.Privacy;

public sealed record PrivacySettingView(Guid ProfileId, string Visibility, bool PublicSharingActive, string DeletionState, DateTime? DeactivationRequestedAtUtc);
