using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Persistence;

internal sealed class EmployerReadStore(EmployerOnboardingDbContext db, IFileStorage storage) : IEmployerReadStore
{
    public async Task<EmployerRegistrationView?> GetRegistrationAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        var r = await db.EmployerRegistrations.AsNoTracking().FirstOrDefaultAsync(x => x.EmployerAccountId == employerAccountId, ct);
        return r is null ? null : ToView(r);
    }

    public async Task<PagedResult<EmployerRegistrationView>> ListRegistrationsAsync(string? status, PageRequest page, CancellationToken ct = default)
    {
        var query = db.EmployerRegistrations.AsNoTracking().AsQueryable();
        if (status is { Length: > 0 } && Enum.TryParse<RegistrationStatus>(status, true, out var parsed))
        {
            query = query.Where(r => r.Status == parsed);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(r => r.OpenedAtUtc).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<EmployerRegistrationView>(items.Select(ToView).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<CompanyMediaView>> ListMediaAsync(Guid employerAccountId, CancellationToken ct = default) =>
        await db.CompanyMedia.AsNoTracking().Where(m => m.EmployerAccountId == employerAccountId && !m.IsRemoved)
            .Select(m => new CompanyMediaView(m.Id, m.Kind.ToString(), m.File.FileName, m.File.ContentType, m.File.SizeBytes, m.IsPrimaryLogo, m.UploadedAtUtc))
            .ToListAsync(ct);

    public async Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        var s = await db.EmployerStandings.AsNoTracking().FirstOrDefaultAsync(x => x.EmployerAccountId == employerAccountId, ct);
        return s is null ? null : new EmployerStandingView(s.EmployerAccountId, s.AdmissionApproved, s.IsVerified, s.BadgeLabel(SharedKernel.Common.Enums.Language.En));
    }

    public async Task<CompanyPublicInfoView?> GetCompanyPublicInfoAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        var registration = await db.EmployerRegistrations.AsNoTracking().FirstOrDefaultAsync(r => r.EmployerAccountId == employerAccountId, ct);
        if (registration?.CompanyIdentity is not { } identity)
        {
            return null;
        }

        var logo = await db.CompanyMedia.AsNoTracking()
            .Where(m => m.EmployerAccountId == employerAccountId && m.IsPrimaryLogo && !m.IsRemoved)
            .Select(m => (string?)m.File.StorageKey)
            .FirstOrDefaultAsync(ct);
        return new CompanyPublicInfoView(employerAccountId, identity.Name, logo is null ? null : storage.UrlFor(logo), registration.Level2?.Industry ?? string.Empty,
            registration.Level2?.Size.ToString() ?? string.Empty, registration.Level2?.Website ?? string.Empty);
    }

    private static EmployerRegistrationView ToView(EmployerRegistration r) => new(
        r.Id, r.EmployerAccountId, r.Status.ToString(),
        r.CompanyIdentity is { } identity ? new CompanyIdentityView(identity.Name, identity.CompanyId, identity.RegistrationNumber) : null,
        r.Level2 is { } level2 ? new Level2View(level2.Website, level2.Industry, level2.Size.ToString(), level2.Address.Governorate, level2.Address.City,
            level2.Address.Street, level2.Description) : null,
        r.OpenedAtUtc, r.ApprovedBy, r.ApprovedAtUtc, r.RowVersion);
}
