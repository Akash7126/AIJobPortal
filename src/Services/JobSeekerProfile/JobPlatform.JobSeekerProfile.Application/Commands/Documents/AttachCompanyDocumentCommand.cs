using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Documents;

public sealed record AttachCompanyDocumentCommand(UploadedFile File, string DocumentType) : EmployerCommand<DocumentView>;
