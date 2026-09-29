using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Profile;

public sealed record UpdateCertificatesCommand(IReadOnlyList<CertificateInput> Entries, string? IfMatch) : JobSeekerCommand<Unit>;
