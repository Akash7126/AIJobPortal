using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;
using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Threshold;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.CandidateSourcing.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real state.</summary>
public sealed class FakeStore : ITalentPoolEntryRepository, IQualificationThresholdRepository, ICandidateInsightRepository, ICandidateProjectionRepository,
    IVerifiedEmployerRepository, ICandidateSourcingReadStore
{
    public List<TalentPoolEntry> Entries { get; } = new();
    public List<QualificationThreshold> Thresholds { get; } = new();
    public List<CandidateInsight> Insights { get; } = new();
    public List<CandidateProjection> Projections { get; } = new();
    public List<VerifiedEmployer> VerifiedEmployers { get; } = new();

    public Task<TalentPoolEntry?> GetAsync(Guid employerAccountId, Guid candidateProfileId, Guid jobPostingId, CancellationToken ct = default) =>
        Task.FromResult(Entries.FirstOrDefault(e =>
            e.EmployerAccountId == employerAccountId && e.CandidateProfileId == candidateProfileId && e.JobPostingId == jobPostingId && !e.Removed));

    public Task<TalentPoolEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Entries.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<TalentPoolEntry>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TalentPoolEntry>>(Entries.Where(e => e.EmployerAccountId == employerAccountId && !e.Removed).ToList());

    void ITalentPoolEntryRepository.Add(TalentPoolEntry entry) => Entries.Add(entry);

    public Task<QualificationThreshold?> GetAsync(Guid employerAccountId, Guid jobPostingId, CancellationToken ct = default) =>
        Task.FromResult(Thresholds.FirstOrDefault(t => t.EmployerAccountId == employerAccountId && t.JobPostingId == jobPostingId));

    void IQualificationThresholdRepository.Add(QualificationThreshold threshold) => Thresholds.Add(threshold);

    void ICandidateInsightRepository.Add(CandidateInsight insight) => Insights.Add(insight);

    public Task<CandidateProjection?> GetAsync(Guid profileId, CancellationToken ct = default) =>
        Task.FromResult(Projections.FirstOrDefault(p => p.ProfileId == profileId));

    public Task<CandidateProjection?> GetByOwnerAccountIdAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Projections.FirstOrDefault(p => p.OwnerAccountId == ownerAccountId));

    void ICandidateProjectionRepository.Add(CandidateProjection projection) => Projections.Add(projection);

    public Task<bool> IsVerifiedAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(VerifiedEmployers.Any(e => e.EmployerAccountId == employerAccountId));

    Task<VerifiedEmployer?> IVerifiedEmployerRepository.GetAsync(Guid employerAccountId, CancellationToken ct) =>
        Task.FromResult(VerifiedEmployers.FirstOrDefault(e => e.EmployerAccountId == employerAccountId));

    void IVerifiedEmployerRepository.Add(VerifiedEmployer employer) => VerifiedEmployers.Add(employer);

    public Task<IReadOnlyList<TalentPoolEntryView>> ListTalentPoolAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TalentPoolEntryView>>(Entries.Where(e => e.EmployerAccountId == employerAccountId && !e.Removed)
            .Select(e => new TalentPoolEntryView(e.Id, e.CandidateProfileId, e.JobPostingId, e.Note, e.AddedAtUtc)).ToList());

    public Task<PagedResult<CandidateSearchResultItemView>> SearchCandidatesAsync(CandidateSearchCriteria criteria, PageRequest page, CancellationToken ct = default)
    {
        var items = Projections.Where(p => !p.Deactivated)
            .Select(p => new CandidateSearchResultItemView(p.ProfileId, p.Skills, p.EducationLevel, p.YearsOfExperience, p.LocationCode, p.SalaryMin, p.SalaryMax))
            .ToList();
        return Task.FromResult(new PagedResult<CandidateSearchResultItemView>(items.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, items.Count));
    }
}

public sealed class FakeCache : ICandidateSourcingCache
{
    public Dictionary<string, object> Items { get; } = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult(Items.TryGetValue(key, out var v) ? (T?)v : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        Items[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        Items.Remove(key);
        return Task.CompletedTask;
    }
}

public static class Kit
{
    public static readonly Guid EmployerId = Guid.NewGuid();

    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType? actor = ActorType.Employer, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? EmployerId);
        return user;
    }
}
