using JobPlatform.JobSeekerProfile.Application;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence;

/// <summary>Read side: AsNoTracking projections (foundation section 3.5).</summary>
internal sealed class ProfileReadStore(JobSeekerProfileDbContext db) : IProfileReadStore
{
    public async Task<ProfileView?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default)
    {
        var profile = await Full().AsNoTracking().FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);
        return profile is null ? null : ToView(profile);
    }

    public async Task<ProfileView?> GetByIdAsync(Guid profileId, CancellationToken ct = default)
    {
        var profile = await Full().AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId, ct);
        return profile is null ? null : ToView(profile);
    }

    public async Task<SharedProfileView?> GetSharedAsync(Guid profileId, CancellationToken ct = default)
    {
        var profile = await Full().AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId && p.Status == ProfileStatus.Active, ct);
        if (profile is null)
        {
            return null;
        }

        return new SharedProfileView(profile.FullName.Value, profile.Skills.Select(ToView).ToList(), profile.Education.Select(ToView).ToList(),
            profile.Experience.Select(ToView).ToList(), profile.Statement, profile.Bio);
    }

    public async Task<IReadOnlyList<DocumentView>> ListDocumentsAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct = default) =>
        (await db.Documents.AsNoTracking().Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId).ToListAsync(ct))
        .Select(d => new DocumentView(d.Id, d.OwnerType.ToString(), d.OwnerId, d.File.FileName, d.File.SizeBytes, d.File.ContentType, d.DocumentType,
            d.UploadedAtUtc)).ToList();

    public async Task<ResumeView?> GetCurrentResumeAsync(Guid profileId, CancellationToken ct = default)
    {
        var resume = await db.Resumes.AsNoTracking().FirstOrDefaultAsync(r => r.ProfileId == profileId && r.IsCurrent, ct);
        return resume is null ? null : new ResumeView(resume.Id, resume.ProfileId, resume.File.FileName, resume.File.SizeBytes, resume.Format.ToString(),
            resume.UploadedAtUtc);
    }

    private IQueryable<Profile> Full() => db.Profiles
        .Include(p => p.Education).Include(p => p.Experience).Include(p => p.Skills).Include(p => p.Training).Include(p => p.Certificates)
        .Include(p => p.SocialLinks);

    private static ProfileView ToView(Profile p) => new(p.Id, p.OwnerAccountId, p.Status.ToString(), p.FullName.Value, p.Email.Value, p.MobileNumber.Value,
        p.Gender.ToString(), p.Education.Select(ToView).ToList(), p.Experience.Select(ToView).ToList(), p.Skills.Select(ToView).ToList(),
        p.Training.Select(t => new TrainingView(t.Id, t.Name, t.Provider, t.CompletedOn)).ToList(),
        p.Certificates.Select(c => new CertificateView(c.Id, c.Name, c.Issuer, c.IssuedOn)).ToList(),
        p.SalaryExpectation is null ? null : new SalaryRangeView(p.SalaryExpectation.Min, p.SalaryExpectation.Max, p.SalaryExpectation.Currency),
        p.Address is null ? null : new AddressView(p.Address.Governorate, p.Address.City, p.Address.Street), p.YearsOfExperience,
        p.SocialLinks.Select(s => new SocialLinkView(s.Network, s.Url)).ToList(), p.Statement, p.Bio, p.CompletionPercent, p.RowVersion);

    private static EducationView ToView(EducationEntry e) => new(e.Id, e.Degree, e.Institution, e.From, e.To, e.Source.ToString());
    private static ExperienceView ToView(ExperienceEntry e) => new(e.Id, e.Company, e.Role, e.From, e.To, e.Source.ToString());
    private static SkillView ToView(SkillEntry s) => new(s.Id, s.Name, s.Kind.ToString(), s.Class.ToString(), s.Source.ToString());
}
