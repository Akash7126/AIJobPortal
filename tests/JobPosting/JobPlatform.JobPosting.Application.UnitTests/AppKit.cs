using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.JobPosting.Application.Interfaces;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using JobPlatform.JobPosting.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.JobPosting.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real aggregate state.</summary>
public sealed class FakeStore : IJobPostingRepository, IFavoriteJobListRepository, ISavedSearchRepository, IInterestedListRepository
{
    public List<JobPlatform.JobPosting.Domain.JobPosting> Postings { get; } = new();
    public List<FavoriteJobList> Favorites { get; } = new();
    public List<SavedSearch> Searches { get; } = new();
    public List<InterestedListEntry> Entries { get; } = new();

    public Task<JobPlatform.JobPosting.Domain.JobPosting?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Postings.FirstOrDefault(p => p.Id == id));

    public Task<JobPlatform.JobPosting.Domain.JobPosting?> GetDraftByHashAsync(Guid employerAccountId, string contentHash, CancellationToken ct = default) =>
        Task.FromResult(Postings.FirstOrDefault(p => p.EmployerAccountId == employerAccountId && p.ContentHash == contentHash && p.Status == JobPostingStatus.Draft));

    public Task<JobPlatform.JobPosting.Domain.JobPosting?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default) =>
        Task.FromResult(Postings.FirstOrDefault(p => p.Source.PlatformJobId == platformJobId));

    public Task<IReadOnlyList<JobPlatform.JobPosting.Domain.JobPosting>> ListDueForExpiryAsync(DateTime nowUtc, int batchSize, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<JobPlatform.JobPosting.Domain.JobPosting>>(Postings
            .Where(p => (p.Status == JobPostingStatus.Active || p.Status == JobPostingStatus.Paused) && p.Deadline.AutoClose && p.Deadline.DateUtc <= nowUtc)
            .Take(batchSize).ToArray());

    public Task<JobPlatform.JobPosting.Domain.JobPosting?> GetForMatchEvaluationAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);

    public void Add(JobPlatform.JobPosting.Domain.JobPosting posting) => Postings.Add(posting);

    public Task<FavoriteJobList?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Favorites.FirstOrDefault(f => f.OwnerAccountId == ownerAccountId));

    public void Add(FavoriteJobList list) => Favorites.Add(list);

    public Task<SavedSearch?> GetByHashAsync(Guid ownerAccountId, string criteriaHash, CancellationToken ct = default) =>
        Task.FromResult(Searches.FirstOrDefault(s => s.OwnerAccountId == ownerAccountId && s.CriteriaHash == criteriaHash));

    Task<SavedSearch?> ISavedSearchRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Searches.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<SavedSearch>> ListByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SavedSearch>>(Searches.Where(s => s.OwnerAccountId == ownerAccountId).ToArray());

    public Task<IReadOnlyList<SavedSearch>> ListNotifyingAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SavedSearch>>(Searches.Where(s => s.NotifyOnMatch).ToArray());

    public void Add(SavedSearch search) => Searches.Add(search);

    public void Remove(SavedSearch search) => Searches.Remove(search);

    Task<IReadOnlyList<InterestedListEntry>> IInterestedListRepository.ListByOwnerAsync(Guid ownerAccountId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InterestedListEntry>>(Entries.Where(e => e.OwnerAccountId == ownerAccountId).ToArray());

    Task<InterestedListEntry?> IInterestedListRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Entries.FirstOrDefault(e => e.Id == id));

    public void Add(InterestedListEntry entry) => Entries.Add(entry);

    public void Remove(InterestedListEntry entry) => Entries.Remove(entry);
}

/// <summary>Always-valid schema (no taxonomy whitelist), matching the standalone/dev fallback of the real Infrastructure adapter.</summary>
public sealed class FakeSchemaValidator : IJobPostingSchemaValidator
{
    public bool ShouldTimeOut { get; set; }
    public int Version { get; set; } = 1;
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();

    public Task<SchemaValidationResult> ValidateAsync(JobPostingFields fields, CancellationToken ct = default) =>
        ShouldTimeOut
            ? throw new TaxonomyUnavailableException("taxonomy unavailable")
            : Task.FromResult(new SchemaValidationResult(Errors.Count == 0, Version, Errors));
}

public sealed class FakeEmployerStanding : IEmployerStandingProvider
{
    public bool? Approved { get; set; } = true;

    public Task<bool?> IsApprovedAsync(Guid employerAccountId, CancellationToken ct = default) => Task.FromResult(Approved);
}

public sealed class FakeMatchRanking : IMatchRankingProvider
{
    public IReadOnlyList<MatchRankingItemView> Items { get; set; } = Array.Empty<MatchRankingItemView>();

    public Task<IReadOnlyList<MatchRankingItemView>> GetRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(Items);
}

public sealed class FakeSearchReadModel : IJobPostingSearchReadModel
{
    public Task<PagedResult<JobPostingSummaryView>> SearchAsync(SearchCriteriaInput criteria, string? sort, PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<JobPostingSummaryView>(Array.Empty<JobPostingSummaryView>(), page.Page, page.PageSize, 0));

    public Task<JobPostingView?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<JobPostingView?>(null);

    public Task<PagedResult<JobPostingSummaryView>> ListByEmployerAsync(Guid employerAccountId, string? status, PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<JobPostingSummaryView>(Array.Empty<JobPostingSummaryView>(), page.Page, page.PageSize, 0));

    public Task<PagedResult<JobPostingSummaryView>> ListOpenByEmployerAsync(Guid employerAccountId, PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<JobPostingSummaryView>(Array.Empty<JobPostingSummaryView>(), page.Page, page.PageSize, 0));

    public Task<IReadOnlyList<string>> CheckReferenceUsageAsync(string type, IReadOnlyCollection<string> codes, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<JobPostingSchemaView> GetSchemaAsync(CancellationToken ct = default) =>
        Task.FromResult(new JobPostingSchemaView(1, Array.Empty<JobPostingSchemaFieldView>(), Array.Empty<string>(), Array.Empty<string>()));
}

public static class Kit
{
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType actor = ActorType.Employer, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? Guid.NewGuid());
        return user;
    }
}
