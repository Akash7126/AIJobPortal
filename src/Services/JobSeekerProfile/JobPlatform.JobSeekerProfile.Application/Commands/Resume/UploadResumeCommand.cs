using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Application.DTOs.Resume;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Resume;

public sealed record UploadResumeCommand(UploadedFile File) : JobSeekerCommand<ResumeView>;
