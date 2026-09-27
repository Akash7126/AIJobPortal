using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.EmployerOnboarding.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real aggregate state (no mocking of domain behaviour).</summary>
public sealed class FakeStore : IEmployerRegistrationRepository, ICompanyMediaRepository, IEmployerStandingRepository, IKnownAccountRepository
{
    public List<EmployerRegistration> Registrations { get; } = new();
    public List<CompanyMediaAndDocument> Media { get; } = new();
    public List<EmployerStanding> Standings { get; } = new();
    public List<KnownAccount> KnownAccounts { get; } = new();

    Task<EmployerRegistration?> IEmployerRegistrationRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Registrations.FirstOrDefault(r => r.Id == id));

    public Task<EmployerRegistration?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Registrations.FirstOrDefault(r => r.EmployerAccountId == employerAccountId));

    void IEmployerRegistrationRepository.Add(EmployerRegistration registration) => Registrations.Add(registration);

    Task<CompanyMediaAndDocument?> ICompanyMediaRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Media.FirstOrDefault(m => m.Id == id));

    public Task<CompanyMediaAndDocument?> GetByHashAsync(Guid employerAccountId, string sha256, CancellationToken ct = default) =>
        Task.FromResult(Media.FirstOrDefault(m => m.EmployerAccountId == employerAccountId && m.File.Sha256 == sha256 && !m.IsRemoved));

    public Task<IReadOnlyList<CompanyMediaAndDocument>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CompanyMediaAndDocument>>(Media.Where(m => m.EmployerAccountId == employerAccountId && !m.IsRemoved).ToList());

    void ICompanyMediaRepository.Add(CompanyMediaAndDocument media) => Media.Add(media);

    public Task<EmployerStanding?> GetAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Standings.FirstOrDefault(s => s.EmployerAccountId == employerAccountId));

    void IEmployerStandingRepository.Add(EmployerStanding standing) => Standings.Add(standing);

    Task<KnownAccount?> IKnownAccountRepository.GetAsync(Guid accountId, CancellationToken ct) =>
        Task.FromResult(KnownAccounts.FirstOrDefault(a => a.AccountId == accountId));

    void IKnownAccountRepository.Add(KnownAccount account) => KnownAccounts.Add(account);
}

public sealed class FakeReadStore : IEmployerReadStore
{
    public Func<Guid, EmployerRegistrationView?> Registration { get; set; } = _ => null;
    public Func<Guid, IReadOnlyList<CompanyMediaView>> MediaList { get; set; } = _ => Array.Empty<CompanyMediaView>();
    public Func<Guid, EmployerStandingView?> Standing { get; set; } = _ => null;
    public Func<Guid, CompanyPublicInfoView?> Company { get; set; } = _ => null;

    public Task<EmployerRegistrationView?> GetRegistrationAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Registration(employerAccountId));

    public Task<PagedResult<EmployerRegistrationView>> ListRegistrationsAsync(string? status, PageRequest page, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<EmployerRegistrationView>(Array.Empty<EmployerRegistrationView>(), page.Page, page.PageSize, 0));

    public Task<IReadOnlyList<CompanyMediaView>> ListMediaAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(MediaList(employerAccountId));

    public Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default) => Task.FromResult(Standing(employerAccountId));

    public Task<CompanyPublicInfoView?> GetCompanyPublicInfoAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Company(employerAccountId));
}

public sealed class FakeFileStorage : IFileStorage
{
    public Task<string> SaveAsync(Stream content, string suggestedFileName, string contentType, CancellationToken ct = default) =>
        Task.FromResult("stored/" + suggestedFileName);

    public string UrlFor(string storageKey) => "https://files.local/" + storageKey;
}

public sealed class FakeMalwareScanner : IMalwareScanner
{
    public bool NextResultClean { get; set; } = true;

    public Task<ScanResult> ScanAsync(Stream content, string contentType, CancellationToken ct = default) =>
        Task.FromResult(new ScanResult(NextResultClean, NextResultClean ? null : "Infected."));
}

public sealed class FakeEmployerCache : IEmployerCache
{
    public Dictionary<Guid, EmployerStandingView> Standings { get; } = new();
    public Dictionary<Guid, CompanyPublicInfoView> Companies { get; } = new();

    public Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Standings.GetValueOrDefault(employerAccountId));

    public Task SetStandingAsync(Guid employerAccountId, EmployerStandingView view, CancellationToken ct = default)
    {
        Standings[employerAccountId] = view;
        return Task.CompletedTask;
    }

    public Task<CompanyPublicInfoView?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Companies.GetValueOrDefault(employerAccountId));

    public Task SetCompanyAsync(Guid employerAccountId, CompanyPublicInfoView view, CancellationToken ct = default)
    {
        Companies[employerAccountId] = view;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        Standings.Remove(employerAccountId);
        Companies.Remove(employerAccountId);
        return Task.CompletedTask;
    }
}

public static class Kit
{
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType? actor = ActorType.Employer, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? Guid.NewGuid());
        user.MfaVerified.Returns(actor == ActorType.Administrator);
        return user;
    }
}
