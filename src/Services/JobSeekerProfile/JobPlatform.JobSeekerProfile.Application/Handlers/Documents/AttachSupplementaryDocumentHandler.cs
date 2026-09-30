using JobPlatform.JobSeekerProfile.Application.Commands.Documents;
using JobPlatform.JobSeekerProfile.Application.Documents;
using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Application.Interfaces;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Documents;

internal sealed class AttachSupplementaryDocumentHandler(IProfileRepository profiles, ISupplementaryDocumentRepository documents, IFileStorage storage,
    IMalwareScanner scanner, ICurrentUser user, TimeProvider clock) : ICommandHandler<AttachSupplementaryDocumentCommand, DocumentView>
{
    public async Task<Result<DocumentView>> Handle(AttachSupplementaryDocumentCommand request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var (storageKey, sha256) = await DocumentSupport.StoreAsync(storage, scanner, $"profiles/{profile.Id}/documents", request.File, ct);
        var existing = await documents.GetByHashAsync(DocumentOwnerType.JobSeekerProfile, profile.OwnerAccountId, sha256, ct);
        if (existing is not null)
        {
            return DocumentSupport.ToView(existing);
        }

        var file = new FileReference(storageKey, request.File.FileName, request.File.SizeBytes, request.File.ContentType, sha256);
        var document = SupplementaryDocument.Attach(Guid.NewGuid(), DocumentOwnerType.JobSeekerProfile, profile.OwnerAccountId, file, request.DocumentType,
            clock.GetUtcNow().UtcDateTime);
        documents.Add(document);
        return DocumentSupport.ToView(document);
    }
}
