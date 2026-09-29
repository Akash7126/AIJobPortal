namespace JobPlatform.AiMatching.Application;

public static class ResumeRules
{
    public const long MaxBytes = 10L * 1024 * 1024;
    public static readonly string[] Formats = { "PDF", "DOCX", "TXT" };

    public static bool IsSupportedFormat(string? format) => Formats.Contains(format?.Trim().ToUpperInvariant());
}
