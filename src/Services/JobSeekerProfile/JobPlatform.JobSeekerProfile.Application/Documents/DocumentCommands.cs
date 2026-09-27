using System.Security.Cryptography;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Documents;

/// <summary>The multipart body itself is read by the Api layer into a seekable stream before the command is built (foundation: Application has no HTTP types).</summary>
public sealed record UploadedFile(string FileName, string ContentType, long SizeBytes, Stream Content);

public sealed record AttachSupplementaryDocumentCommand(UploadedFile File, string DocumentType) : JobSeekerCommand<DocumentView>;
public sealed record AttachCompanyDocumentCommand(UploadedFile File, string DocumentType) : EmployerCommand<DocumentView>;
public sealed record RemoveSupplementaryDocumentCommand(Guid DocumentId) : JobSeekerCommand<Unit>;
public sealed record ListSupplementaryDocumentsQuery : JobSeekerQuery<IReadOnlyList<DocumentView>>;

internal static class DocumentSupport
{
    public static async Task<(string StorageKey, string Sha256)> StoreAsync(IFileStorage storage, IMalwareScanner scanner, string folder, UploadedFile file,
        CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(buffer, ct));
        buffer.Position = 0;
        if (!await scanner.IsCleanAsync(buffer, ct))
        {
            throw new InvalidOperationException("The uploaded file failed the malware scan.");
        }

        buffer.Position = 0;
        var storageKey = await storage.SaveAsync(folder, file.FileName, buffer, ct);
        return (storageKey, sha256);
    }

    public static DocumentView ToView(SupplementaryDocument document) => new(document.Id, document.OwnerType.ToString(), document.OwnerId,
        document.File.FileName, document.File.SizeBytes, document.File.ContentType, document.DocumentType, document.UploadedAtUtc);
}

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

internal sealed class ListSupplementaryDocumentsHandler(IProfileRepository profiles, IProfileReadStore reads, ICurrentUser user)
    : IQueryHandler<ListSupplementaryDocumentsQuery, IReadOnlyList<DocumentView>>
{
    public async Task<Result<IReadOnlyList<DocumentView>>> Handle(ListSupplementaryDocumentsQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        return Result.Success(await reads.ListDocumentsAsync(DocumentOwnerType.JobSeekerProfile, profile.OwnerAccountId, ct));
    }
}
