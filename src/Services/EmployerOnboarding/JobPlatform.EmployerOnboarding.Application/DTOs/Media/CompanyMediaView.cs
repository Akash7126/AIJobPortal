namespace JobPlatform.EmployerOnboarding.Application.DTOs.Media;

public sealed record CompanyMediaView(Guid CompanyMediaId, string Kind, string FileName, string ContentType, long SizeBytes, bool IsPrimaryLogo, DateTime UploadedAtUtc);
