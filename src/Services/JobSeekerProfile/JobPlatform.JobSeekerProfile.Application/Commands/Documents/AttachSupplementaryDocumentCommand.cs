using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Documents;

public sealed record AttachSupplementaryDocumentCommand(UploadedFile File, string DocumentType) : JobSeekerCommand<DocumentView>;
