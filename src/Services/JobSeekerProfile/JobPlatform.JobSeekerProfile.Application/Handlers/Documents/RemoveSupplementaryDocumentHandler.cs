using JobPlatform.JobSeekerProfile.Application.Commands.Documents;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Documents;

internal sealed class RemoveSupplementaryDocumentHandler(ISupplementaryDocumentRepository documents, ICurrentUser user)
    : ICommandHandler<RemoveSupplementaryDocumentCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RemoveSupplementaryDocumentCommand request, CancellationToken ct)
    {
        var document = await documents.GetByIdAsync(request.DocumentId, ct);
        if (document is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The document was not found.");
        }

        document.EnsureOwnedBy(new Actor(user.UserId!.Value));
        documents.Remove(document);
        return Result.Success();
    }
}
