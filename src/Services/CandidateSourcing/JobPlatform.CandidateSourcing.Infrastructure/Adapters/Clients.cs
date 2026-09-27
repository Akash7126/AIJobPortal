using System.Collections.Concurrent;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

namespace JobPlatform.CandidateSourcing.Infrastructure.Adapters;

// ---------------------------------------------------------------------- BC-09 Job Posting

/// <summary>Adapter (JobPostingClient:Provider = Http) over BC-09's internal API. BC-09 is not necessarily reachable yet; a failure is treated as
/// "not found" by the caller through the null contract - the same shape BC-09 itself returns for a missing posting.</summary>
internal sealed class HttpJobPostingApiClient(HttpClient http) : IJobPostingApi
{
    public Task<PostingForMatchingDto?> GetPostingForMatchingAsync(Guid jobPostingId, CancellationToken ct = default) =>
        http.GetOrNullAsync<PostingForMatchingDto>($"internal/v1/postings/{jobPostingId}", ct);
}

/// <summary>Local double (JobPostingClient:Provider = Fake, the development default): an in-memory registry seeded by tests / dev tooling.</summary>
internal sealed class FakeJobPostingApiClient : IJobPostingApi
{
    private static readonly ConcurrentDictionary<Guid, PostingForMatchingDto> Postings = new();

    public static void Seed(PostingForMatchingDto posting) => Postings[posting.JobPostingId] = posting;

    public static void Reset() => Postings.Clear();

    public Task<PostingForMatchingDto?> GetPostingForMatchingAsync(Guid jobPostingId, CancellationToken ct = default) =>
        Task.FromResult(Postings.GetValueOrDefault(jobPostingId));
}

// ---------------------------------------------------------------------- BC-04 Job Seeker Profile

internal sealed class HttpJobSeekerProfileApiClient(HttpClient http) : IJobSeekerProfileApi
{
    public Task<ProfileForMatchingDto?> GetProfileForMatchingAsync(Guid profileId, CancellationToken ct = default) =>
        http.GetOrNullAsync<ProfileForMatchingDto>($"internal/v1/profiles/{profileId}", ct);

    public Task<ResumeContentUrlDto?> GetResumeContentUrlAsync(Guid resumeId, CancellationToken ct = default) =>
        http.GetOrNullAsync<ResumeContentUrlDto>($"internal/v1/resumes/{resumeId}/content-url", ct);

    public Task<CandidatePrivacyDto?> GetCandidatePrivacyAsync(Guid profileId, CancellationToken ct = default) =>
        http.GetOrNullAsync<CandidatePrivacyDto>($"internal/v1/profiles/{profileId}/privacy", ct);

    public Task<CandidateViewDto?> GetCandidateViewAsync(Guid profileId, CancellationToken ct = default) =>
        http.GetOrNullAsync<CandidateViewDto>($"internal/v1/profiles/{profileId}/candidate-view", ct);
}

/// <summary>Local double (JobSeekerProfileClient:Provider = Fake, the development default): an in-memory registry seeded by tests / dev tooling.</summary>
internal sealed class FakeJobSeekerProfileApiClient : IJobSeekerProfileApi
{
    private static readonly ConcurrentDictionary<Guid, CandidateViewDto> Views = new();

    public static void Seed(CandidateViewDto view) => Views[view.ProfileId] = view;

    public static void Reset() => Views.Clear();

    public Task<ProfileForMatchingDto?> GetProfileForMatchingAsync(Guid profileId, CancellationToken ct = default) => Task.FromResult<ProfileForMatchingDto?>(null);

    public Task<ResumeContentUrlDto?> GetResumeContentUrlAsync(Guid resumeId, CancellationToken ct = default) => Task.FromResult<ResumeContentUrlDto?>(null);

    public Task<CandidatePrivacyDto?> GetCandidatePrivacyAsync(Guid profileId, CancellationToken ct = default)
    {
        if (!Views.TryGetValue(profileId, out var view))
        {
            return Task.FromResult<CandidatePrivacyDto?>(null);
        }

        return Task.FromResult<CandidatePrivacyDto?>(new CandidatePrivacyDto(view.ProfileId, view.Visibility, view.EmployerVisibilityOptIn, view.Deactivated,
            view.DisclosedFields, view.UpdatedAtUtc));
    }

    public Task<CandidateViewDto?> GetCandidateViewAsync(Guid profileId, CancellationToken ct = default) =>
        Task.FromResult(Views.GetValueOrDefault(profileId));
}

// ---------------------------------------------------------------------- BC-10 AI Matching

internal sealed class HttpAiMatchingApiClient(HttpClient http) : IAiMatchingApi
{
    public async Task<MatchScoreListDto> ListMatchScoresAsync(Guid jobPostingId, decimal? minScore, int page, int pageSize, CancellationToken ct = default)
    {
        var query = $"internal/v1/match-scores?jobPostingId={jobPostingId}&page={page}&pageSize={pageSize}" + (minScore is { } m ? $"&minScore={m}" : string.Empty);
        return await http.GetOrNullAsync<MatchScoreListDto>(query, ct) ?? new MatchScoreListDto(Array.Empty<MatchScoreDto>(), page, pageSize, 0, 0, "n/a");
    }

    public async Task<MatchRankingDto> GetMatchRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default) =>
        await http.GetOrNullAsync<MatchRankingDto>($"internal/v1/match-ranking?profileId={profileId}&page={page}&pageSize={pageSize}", ct)
        ?? new MatchRankingDto(Array.Empty<MatchRankingItemDto>(), page, pageSize, 0, 0);

    public Task<ResumeParsedDataDto?> GetResumeParsedDataAsync(Guid resumeParsedDataId, CancellationToken ct = default) =>
        http.GetOrNullAsync<ResumeParsedDataDto>($"internal/v1/resume-parsed-data/{resumeParsedDataId}", ct);
}

/// <summary>Local double (AiMatchingClient:Provider = Fake, the development default): an in-memory registry seeded by tests / dev tooling.</summary>
internal sealed class FakeAiMatchingApiClient : IAiMatchingApi
{
    private static readonly ConcurrentDictionary<Guid, List<MatchScoreDto>> ScoresByPosting = new();

    public static void Seed(Guid jobPostingId, MatchScoreDto score) =>
        ScoresByPosting.AddOrUpdate(jobPostingId, _ => new List<MatchScoreDto> { score }, (_, list) =>
        {
            list.RemoveAll(s => s.ProfileId == score.ProfileId);
            list.Add(score);
            return list;
        });

    public static void Reset() => ScoresByPosting.Clear();

    public Task<MatchScoreListDto> ListMatchScoresAsync(Guid jobPostingId, decimal? minScore, int page, int pageSize, CancellationToken ct = default)
    {
        var all = ScoresByPosting.GetValueOrDefault(jobPostingId) ?? new List<MatchScoreDto>();
        var filtered = minScore is { } min ? all.Where(s => s.Score >= min).ToList() : all;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(new MatchScoreListDto(items, page, pageSize, filtered.Count, 0m, "fake-v1"));
    }

    public Task<MatchRankingDto> GetMatchRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(new MatchRankingDto(Array.Empty<MatchRankingItemDto>(), page, pageSize, 0, 0m));

    public Task<ResumeParsedDataDto?> GetResumeParsedDataAsync(Guid resumeParsedDataId, CancellationToken ct = default) =>
        Task.FromResult<ResumeParsedDataDto?>(null);
}
