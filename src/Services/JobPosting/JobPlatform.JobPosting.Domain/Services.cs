using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JobPlatform.JobPosting.Domain;

public sealed record SchemaValidationResult(bool IsValid, int TaxonomyVersion, IReadOnlyList<string> Errors);

/// <summary>The taxonomy service (BC-08) could not be reached within the retry budget.</summary>
public sealed class TaxonomyUnavailableException : Exception
{
    public TaxonomyUnavailableException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>Deterministic content hash used for INV-03 (identical-draft de-duplication) and saved-search identity (foundation section 8).</summary>
public static class ContentHasher
{
    public static string Hash(JobPostingFields fields)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            titleEn = fields.Title.En.Trim().ToLowerInvariant(),
            titleAr = fields.Title.Ar.Trim().ToLowerInvariant(),
            summaryEn = fields.Summary.En.Trim().ToLowerInvariant(),
            summaryAr = fields.Summary.Ar.Trim().ToLowerInvariant(),
            skills = fields.Skills.Select(s => s.ToLowerInvariant()).OrderBy(s => s, StringComparer.Ordinal),
            fields.CategoryCode,
            contractType = fields.ContractType.ToString(),
            workFormat = fields.WorkFormat.ToString(),
            location = fields.Location is null ? null : $"{fields.Location.Governorate}/{fields.Location.City}",
            fields.JobLink
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
