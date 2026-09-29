namespace JobPlatform.JobSeekerProfile.Application.DTOs.Documents;

public sealed record DocumentView(Guid DocumentId, string OwnerType, Guid OwnerId, string FileName, long SizeBytes, string ContentType, string DocumentType,
    DateTime UploadedAtUtc);
