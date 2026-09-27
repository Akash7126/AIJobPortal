using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AiMatching.Application;

// ---------------------------------------------------------------------- consumed APIs (anti-corruption ports, adapters live in Infrastructure)

/// <summary>BC-04 profile data for scoring. Null = the profile does not exist. Throws <see cref="UpstreamUnavailableException"/> when BC-04 cannot be reached.</summary>
public interface IProfileDirectory
{
    Task<ProfileMatchView?> GetMatchViewAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>Text used to embed the profile (headline, skills, training, education, location).</summary>
    Task<string?> GetEmbeddingTextAsync(Guid profileId, CancellationToken ct = default);
}

/// <summary>A posting as BC-09 publishes it for matching, translated into this BC's language.</summary>
public sealed record PostingSource(
    Guid JobPostingId, Guid EmployerAccountId, string Status, bool Suspended, long Version, string Title, string Category, string? Description,
    IReadOnlyList<string> Skills, EducationLevel? EducationLevel, IReadOnlyList<string> Training, string? Governorate, string? City, WorkArrangement Arrangement,
    int? MinExperienceYears, int? MaxExperienceYears, decimal? SalaryMin, decimal? SalaryMax)
{
    public bool IsActive => !Suspended && string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);
}

public interface IPostingDirectory
{
    Task<PostingSource?> GetAsync(Guid jobPostingId, CancellationToken ct = default);
}

public sealed record ResumeContent(byte[] Bytes, string Format, string Sha256, long SizeBytes);

/// <summary>Fetches the resume file through BC-04's short-lived signed URL. Null = the resume is unknown.</summary>
public interface IResumeContentSource
{
    Task<ResumeContent?> FetchAsync(Guid resumeId, CancellationToken ct = default);
}

/// <summary>BC-08 skills taxonomy (cached, a running job keeps the version it started with).</summary>
public interface ISkillTaxonomyProvider
{
    Task<SkillTaxonomy> GetAsync(string? version, CancellationToken ct = default);

    /// <summary>Evicts the cached taxonomy (PlatformTaxonomyUpdated).</summary>
    Task InvalidateAsync(CancellationToken ct = default);
}

// ---------------------------------------------------------------------- AI ports (handover 3.9)

public interface IResumeParser
{
    /// <summary>Extracts fields with per-field confidence. Null when the file is corrupt or unreadable (INV-08).</summary>
    Task<ParserResult?> ParseAsync(byte[] content, string format, CancellationToken ct = default);
}

public interface ISemanticAnalyzer
{
    Task<SemanticAnalysis> AnalyzeAsync(string title, string? description, IReadOnlyList<string> declaredSkills, string category, SkillTaxonomy taxonomy,
        CancellationToken ct = default);
}

public interface IEmbeddingService
{
    string ModelVersion { get; }

    /// <summary>L2-normalised embedding; deterministic for the same text and model version.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}

public interface IVectorIndex
{
    Task UpsertAsync(VectorEntityType type, Guid entityId, float[] vector, string modelVersion, CancellationToken ct = default);

    Task RemoveAsync(VectorEntityType type, Guid entityId, CancellationToken ct = default);

    /// <summary>Nearest neighbours by cosine similarity, best first.</summary>
    Task<IReadOnlyList<VectorHit>> SearchAsync(VectorEntityType type, float[] query, int topK, CancellationToken ct = default);
}

/// <summary>Activity history (views, favourites, saves) for collaborative filtering. No BC publishes it today (Q-06): the default adapter returns nothing, so recommendations are content-only.</summary>
public interface IActivityHistory
{
    /// <summary>Collaborative score 0-100 per posting for this profile; empty when there is not enough history.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetCollaborativeScoresAsync(Guid profileId, IReadOnlyCollection<Guid> candidatePostingIds, CancellationToken ct = default);
}

// ---------------------------------------------------------------------- configuration

public interface IMatchingConfigurationProvider
{
    /// <summary>The current configuration snapshot (cache-aside; falls back to the database, then to the built-in defaults).</summary>
    Task<MatchingConfigSnapshot> GetAsync(CancellationToken ct = default);

    Task InvalidateAsync(CancellationToken ct = default);
}

// ---------------------------------------------------------------------- read side

public sealed record MatchedJobDto(Guid MatchScoreId, Guid JobPostingId, string Title, decimal Score, DateTime ComputedAtUtc);

public sealed record MatchedCandidateDto(Guid MatchScoreId, Guid ProfileId, decimal Score, DateTime ComputedAtUtc);

/// <summary>Read side (foundation section 3.5): projections over the stored scores and replicas, never aggregates.</summary>
public interface IMatchReadStore
{
    /// <summary>Stored scores of a profile for active postings, best first.</summary>
    Task<PagedResult<MatchedJobDto>> ListJobRankingAsync(Guid profileId, decimal minScore, PageRequest page, CancellationToken ct = default);

    /// <summary>Stored scores of a posting for active profiles, best first (ties: lowest profile id).</summary>
    Task<PagedResult<MatchedCandidateDto>> ListCandidatesAsync(Guid jobPostingId, decimal minScore, PageRequest page, CancellationToken ct = default);

    Task<PagedResult<MatchScoreDto>> ListScoresWithBreakdownAsync(Guid jobPostingId, decimal minScore, PageRequest page, CancellationToken ct = default);

    Task<ParsedProfileDataDto?> GetParsedProfileDataAsync(Guid ownerAccountId, CancellationToken ct = default);

    Task<ResumeParsedDataDto?> GetResumeParsedDataAsync(Guid id, CancellationToken ct = default);

    Task<ShortlistDto?> GetShortlistAsync(Guid id, CancellationToken ct = default);

    Task<JobRecommendationDto?> GetLatestRecommendationAsync(Guid profileId, CancellationToken ct = default);

    Task<MatchingConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default);
}

// ---------------------------------------------------------------------- read models

public sealed record WeightsDto(decimal SkillOverlap, decimal Education, decimal Training, decimal Location, decimal Experience, decimal Salary);

public sealed record MatchingConfigurationDto(
    int Version, decimal MatchThresholdPercent, WeightsDto Weights, int ShortlistSize, decimal LowConfidenceThresholdPercent, DateTime UpdatedAtUtc, string ETag);

public sealed record MatchScoreDetailDto(
    Guid? MatchScoreId, Guid JobPostingId, string? Title, decimal Score, bool MeetsThreshold, int ConfigVersion, bool Stored, DateTime ComputedAtUtc,
    IReadOnlyList<MatchCriterionDto> Breakdown);

public sealed record ShortlistEntryDto(int Rank, Guid ProfileId, decimal Score);

public sealed record ShortlistDto(
    Guid Id, Guid JobPostingId, Guid EmployerAccountId, string Status, int RequestedSize, int ConfigVersion, IReadOnlyList<ShortlistEntryDto> Items,
    string? FailureReason, DateTime RequestedAtUtc, DateTime? ComputedAtUtc);

public sealed record RecommendedJobDto(Guid JobPostingId, string Title, decimal Score, string Reason);

public sealed record JobRecommendationDto(Guid Id, string Strategy, DateTime ComputedAtUtc, IReadOnlyList<RecommendedJobDto> Items);

public sealed record ParsedProfileFieldDto(string Name, string Value, string Source, decimal Confidence, bool NeedsReview);

public sealed record ParsedProfileDataDto(
    Guid ParsedProfileDataId, Guid ResumeId, Guid ProfileId, string Status, decimal LowConfidenceThresholdPercent, IReadOnlyList<ParsedProfileFieldDto> Fields);
