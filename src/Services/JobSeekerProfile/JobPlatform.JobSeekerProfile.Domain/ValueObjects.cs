using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

public enum Gender
{
    Male,
    Female,
    Other,
    PreferNotToSay
}

public enum WorkArrangement
{
    Physical,
    Online,
    Hybrid
}

public enum ResumeFormat
{
    Pdf,
    Docx,
    Txt
}

/// <summary>Whether a field/entry was entered by the job seeker or merged from AI resume parsing (never silently overwritten once user-entered).</summary>
public enum DataSource
{
    User,
    ResumeParsing
}

public sealed class FullName : ValueObject
{
    public FullName(string value) => Value = value.Trim();

    public string Value { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

public sealed class Address : ValueObject
{
    public Address(string? governorate, string? city, string? street)
    {
        Governorate = governorate?.Trim();
        City = city?.Trim();
        Street = street?.Trim();
    }

    public string? Governorate { get; }
    public string? City { get; }
    public string? Street { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Governorate;
        yield return City;
        yield return Street;
    }
}

public sealed class SocialLink : ValueObject
{
    public SocialLink(string network, string url)
    {
        Network = network.Trim();
        Url = url.Trim();
    }

    public string Network { get; }
    public string Url { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Network;
        yield return Url;
    }
}

public sealed class SalaryRange : ValueObject
{
    public SalaryRange(decimal? min, decimal? max, string? currency)
    {
        Min = min;
        Max = max;
        Currency = currency?.Trim();
    }

    public decimal? Min { get; }
    public decimal? Max { get; }
    public string? Currency { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Min;
        yield return Max;
        yield return Currency;
    }
}

/// <summary>Uploaded-file metadata; the content itself lives in <see cref="Ports.IFileStorage"/>, keyed by <see cref="StorageKey"/>.</summary>
public sealed class FileReference : ValueObject
{
    public FileReference(string storageKey, string fileName, long sizeBytes, string contentType, string sha256)
    {
        StorageKey = storageKey;
        FileName = fileName;
        SizeBytes = sizeBytes;
        ContentType = contentType;
        Sha256 = sha256;
    }

    public string StorageKey { get; }
    public string FileName { get; }
    public long SizeBytes { get; }
    public string ContentType { get; }
    public string Sha256 { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StorageKey;
        yield return FileName;
        yield return SizeBytes;
        yield return ContentType;
        yield return Sha256;
    }
}

/// <summary>Per-section completion state (US-3.1.1-05).</summary>
public enum SectionState
{
    NotStarted,
    Partial,
    Complete
}

public static class ProfileSections
{
    public const string Level1 = "Level1";
    public const string Education = "Education";
    public const string Experience = "Experience";
    public const string Skills = "Skills";
    public const string Training = "Training";
    public const string Certificates = "Certificates";
    public const string Level3 = "Level3";
    public const string ExtractedData = "ExtractedData";

    public static readonly IReadOnlyList<string> Weighted = new[] { Level1, Education, Experience, Skills, Training, Certificates, Level3 };
}
