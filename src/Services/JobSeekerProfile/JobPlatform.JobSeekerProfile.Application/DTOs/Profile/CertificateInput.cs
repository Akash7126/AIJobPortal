namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record CertificateInput(string Name, string? Issuer, DateTime? IssuedOn);
