namespace JobPlatform.JobSeekerProfile.Application.DTOs.Resume;

public sealed record ResumeView(Guid ResumeId, Guid ProfileId, string FileName, long SizeBytes, string Format, DateTime UploadedAtUtc);
