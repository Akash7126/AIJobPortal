using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Interfaces;

public interface IResumeParser
{
    /// <summary>Extracts fields with per-field confidence. Null when the file is corrupt or unreadable (INV-08).</summary>
    Task<ParserResult?> ParseAsync(byte[] content, string format, CancellationToken ct = default);
}
