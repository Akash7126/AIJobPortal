using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Application.DTOs.CompanyPage;
using JobPlatform.HelpContent.Infrastructure.Persistence;
using JobPlatform.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobPlatform.HelpContent.Api.IntegrationTests;

/// <summary>
/// Hosts the real Help Content API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS). BC-06 consumes no integration
/// events (handover section 5.2), so no inbox wiring is needed - only the outbox publisher. The cross-BC providers (BC-05 company info,
/// BC-09 open postings, BC-04 profile interests) are swapped for controllable in-memory test doubles here: this environment has no BC-05/
/// BC-09/BC-04 instance running, so only the Fake provider path is exercised end to end, exactly like every other cross-BC dependency in
/// this repo (see JobPosting's own Api.IntegrationTests).
/// </summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "HelpContent";

    public TestCompanyDirectoryProvider CompanyDirectory { get; } = new();
    public TestOpenPostingsProvider OpenPostings { get; } = new();
    public TestProfileInterestsProvider ProfileInterests { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ICompanyDirectoryProvider>();
        services.AddSingleton<ICompanyDirectoryProvider>(CompanyDirectory);
        services.RemoveAll<IOpenPostingsProvider>();
        services.AddSingleton<IOpenPostingsProvider>(OpenPostings);
        services.RemoveAll<IProfileInterestsProvider>();
        services.AddSingleton<IProfileInterestsProvider>(ProfileInterests);
    }

    public Task<int> PublishAsync() => PublishOutboxAsync<HelpContentDbContext>();
}

/// <summary>Controllable stand-in for BC-05's company-info + standing internal APIs (handover section 6.2).</summary>
public sealed class TestCompanyDirectoryProvider : ICompanyDirectoryProvider
{
    private readonly Dictionary<Guid, CompanyDirectoryEntry> _entries = new();

    public void Register(Guid employerAccountId, CompanyDirectoryEntry entry) => _entries[employerAccountId] = entry;

    public Task<CompanyDirectoryEntry?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(_entries.GetValueOrDefault(employerAccountId));
}

/// <summary>Controllable stand-in for BC-09's open-postings internal API (Q-05, resolved).</summary>
public sealed class TestOpenPostingsProvider : IOpenPostingsProvider
{
    public IReadOnlyList<OpenPostingView> Items { get; set; } = Array.Empty<OpenPostingView>();
    public bool Degraded { get; set; }

    public Task<(IReadOnlyList<OpenPostingView> Items, bool Degraded)> GetOpenPostingsAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult((Items, Degraded));
}

/// <summary>Controllable stand-in for BC-04's (proposed) profile interest-tags API (Q-06).</summary>
public sealed class TestProfileInterestsProvider : IProfileInterestsProvider
{
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

    public Task<IReadOnlyList<string>> GetInterestTagsAsync(Guid jobSeekerAccountId, CancellationToken ct = default) => Task.FromResult(Tags);
}
