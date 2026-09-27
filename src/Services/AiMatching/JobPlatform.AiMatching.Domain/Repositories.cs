namespace JobPlatform.AiMatching.Domain;

/// <summary>Aggregate-oriented repositories (foundation section 8). The unit of work commits; the read side uses IMatchReadStore instead.</summary>
public interface IMatchingConfigurationRepository
{
    /// <summary>The singleton configuration, or null before it is seeded.</summary>
    Task<MatchingConfiguration?> GetCurrentAsync(CancellationToken ct = default);

    void Add(MatchingConfiguration configuration);
}

public interface IMatchScoreRepository
{
    Task<MatchScore?> GetPairAsync(Guid profileId, Guid jobPostingId, CancellationToken ct = default);

    Task<IReadOnlyList<MatchScore>> ListByPostingAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<IReadOnlyList<MatchScore>> ListByProfileAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>Removes the stored scores of a posting that is no longer active (WithdrawPosting).</summary>
    Task<int> RemoveByPostingAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(MatchScore score);
}

public interface IResumeParsedDataRepository
{
    Task<ResumeParsedData?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The current (not superseded) parse of a resume, if any.</summary>
    Task<ResumeParsedData?> GetCurrentByResumeAsync(Guid resumeId, CancellationToken ct = default);

    /// <summary>Latest not-superseded parse of a profile (any resume).</summary>
    Task<ResumeParsedData?> GetLatestByProfileAsync(Guid profileId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid resumeId, string sha256, CancellationToken ct = default);

    void Add(ResumeParsedData data);
}

public interface ISkillStandardizationRepository
{
    Task<SkillStandardization?> GetByResumeParsedDataAsync(Guid resumeParsedDataId, CancellationToken ct = default);

    Task<IReadOnlyList<SkillStandardization>> ListNotOnTaxonomyVersionAsync(string taxonomyVersion, int take, CancellationToken ct = default);

    void Add(SkillStandardization standardization);
}

public interface IParsedProfileDataRepository
{
    Task<ParsedProfileData?> GetByProfileAsync(Guid profileId, CancellationToken ct = default);

    Task<ParsedProfileData?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);

    void Add(ParsedProfileData data);
}

public interface IJobSemanticsRepository
{
    Task<JobSemantics?> GetAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(JobSemantics semantics);
}

public interface IJobRecommendationRepository
{
    Task<JobRecommendation?> GetLatestAsync(Guid profileId, CancellationToken ct = default);

    void Add(JobRecommendation recommendation);
}

public interface ICandidateShortlistRepository
{
    Task<CandidateShortlist?> GetAsync(Guid id, CancellationToken ct = default);

    void Add(CandidateShortlist shortlist);
}

public interface IKnownProfileRepository
{
    Task<KnownProfile?> GetAsync(Guid profileId, CancellationToken ct = default);

    Task<KnownProfile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);

    Task<IReadOnlyList<KnownProfile>> ListActiveAsync(int skip, int take, CancellationToken ct = default);

    void Add(KnownProfile profile);
}

public interface IKnownPostingRepository
{
    Task<KnownPosting?> GetAsync(Guid postingId, CancellationToken ct = default);

    Task<IReadOnlyList<KnownPosting>> ListActiveAsync(int skip, int take, CancellationToken ct = default);

    void Add(KnownPosting posting);
}

public interface IWorkItemRepository
{
    /// <summary>Enqueues unless an identical (kind, entity) item is already pending (idempotent).</summary>
    Task<bool> EnqueueAsync(WorkItemKind kind, Guid entityId, DateTime nowUtc, CancellationToken ct = default);

    Task<IReadOnlyList<MatchingWorkItem>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    Task<MatchingWorkItem?> GetAsync(Guid id, CancellationToken ct = default);
}
