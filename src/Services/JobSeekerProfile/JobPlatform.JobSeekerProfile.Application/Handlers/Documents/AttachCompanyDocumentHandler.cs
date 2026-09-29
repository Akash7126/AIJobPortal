using JobPlatform.JobSeekerProfile.Application.Commands.Documents;
using JobPlatform.JobSeekerProfile.Application.Documents;
using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Documents;

internal sealed class AttachCompanyDocumentHandler(ISupplementaryDocumentRepository documents, IFileStorage storage, IMalwareScanner scanner,
    ICurrentUser user, TimeProvider clock) : ICommandHandler<AttachCompanyDocumentCommand, DocumentView>
{
    public async Task<Result<DocumentView>> Handle(AttachCompanyDocumentCommand request, CancellationToken ct)
    {
        var employerAccountId = user.UserId!.Value;
        var (storageKey, sha256) = await DocumentSupport.StoreAsync(storage, scanner, $"companies/{employerAccountId}/documents", request.File, ct);
        var existing = await documents.GetByHashAsync(DocumentOwnerType.Company, employerAccountId, sha256, ct);
        if (existing is not null)
        {
            return DocumentSupport.ToView(existing);
        }

        var file = new FileReference(storageKey, request.File.FileName, request.File.SizeBytes, request.File.ContentType, sha256);
        var document = SupplementaryDocument.Attach(Guid.NewGuid(), DocumentOwnerType.Company, employerAccountId, file, request.DocumentType,
            clock.GetUtcNow().UtcDateTime);
        documents.Add(document);
        return DocumentSupport.ToView(document);
    }
}
