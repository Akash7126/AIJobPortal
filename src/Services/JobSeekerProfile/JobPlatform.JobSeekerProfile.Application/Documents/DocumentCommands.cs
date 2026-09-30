using System.Security.Cryptography;
using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Application.Interfaces;
using JobPlatform.JobSeekerProfile.Domain;

namespace JobPlatform.JobSeekerProfile.Application.Documents;

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
