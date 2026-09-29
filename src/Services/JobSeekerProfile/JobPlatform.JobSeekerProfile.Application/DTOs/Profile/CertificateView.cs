namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record CertificateView(Guid Id, string Name, string? Issuer, DateTime? IssuedOn);
