using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Documents;

public sealed record RemoveSupplementaryDocumentCommand(Guid DocumentId) : JobSeekerCommand<Unit>;
