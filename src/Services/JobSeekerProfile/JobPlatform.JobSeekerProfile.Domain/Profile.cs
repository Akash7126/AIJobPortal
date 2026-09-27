using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

/// <summary>GAP-002: the SRS gives no weights; these are proposed and configurable (rebalanced to the four aggregate-owned sections
/// plus Level 3, since JobPreference is a separate aggregate and cannot be summed here).</summary>
public static class CompletionWeights
{
    public const int Level1 = 25;
    public const int Education = 15;
    public const int Experience = 15;
    public const int Skills = 20;
    public const int TrainingAndCertificates = 15;
    public const int Level3 = 10;
}

public enum ProfileStatus
{
    Active,
    Deactivated
}

public sealed class EducationEntry
{
    private EducationEntry()
    {
    }

    internal EducationEntry(Guid id, string degree, string institution, DateTime? from, DateTime? to, DataSource source)
    {
        Id = id;
        Degree = degree;
        Institution = institution;
        From = from;
        To = to;
        Source = source;
    }

    public Guid Id { get; private set; }
    public string Degree { get; private set; } = string.Empty;
    public string Institution { get; private set; } = string.Empty;
    public DateTime? From { get; private set; }
    public DateTime? To { get; private set; }
    public DataSource Source { get; private set; }
}

public sealed class ExperienceEntry
{
    private ExperienceEntry()
    {
    }

    internal ExperienceEntry(Guid id, string company, string role, DateTime? from, DateTime? to, DataSource source)
    {
        Id = id;
        Company = company;
        Role = role;
        From = from;
        To = to;
        Source = source;
    }

    public Guid Id { get; private set; }
    public string Company { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public DateTime? From { get; private set; }
    public DateTime? To { get; private set; }
    public DataSource Source { get; private set; }
}

public enum SkillKind
{
    Primary,
    Secondary
}

public enum SkillClass
{
    Soft,
    Hard
}

public sealed class SkillEntry
{
    private SkillEntry()
    {
    }

    internal SkillEntry(Guid id, string name, SkillKind kind, SkillClass @class, DataSource source)
    {
        Id = id;
        Name = name;
        Kind = kind;
        Class = @class;
        Source = source;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SkillKind Kind { get; private set; }
    public SkillClass Class { get; private set; }
    public DataSource Source { get; private set; }

    internal string NormalizedName => Name.Trim().ToLowerInvariant();
}

public sealed class TrainingEntry
{
    private TrainingEntry()
    {
    }

    internal TrainingEntry(Guid id, string name, string? provider, DateTime? completedOn, DataSource source)
    {
        Id = id;
        Name = name;
        Provider = provider;
        CompletedOn = completedOn;
        Source = source;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Provider { get; private set; }
    public DateTime? CompletedOn { get; private set; }
    public DataSource Source { get; private set; }
}

public sealed class CertificateEntry
{
    private CertificateEntry()
    {
    }

    internal CertificateEntry(Guid id, string name, string? issuer, DateTime? issuedOn, DataSource source)
    {
        Id = id;
        Name = name;
        Issuer = issuer;
        IssuedOn = issuedOn;
        Source = source;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Issuer { get; private set; }
    public DateTime? IssuedOn { get; private set; }
    public DataSource Source { get; private set; }
}

/// <summary>AGG-02 Profile (handover section 3.1): the job seeker's structured record. Owner-only, staged Level 1/2/3 sections.</summary>
public sealed class Profile : AggregateRoot<Guid>
{
    private readonly List<EducationEntry> _education = new();
    private readonly List<ExperienceEntry> _experience = new();
    private readonly List<SkillEntry> _skills = new();
    private readonly List<TrainingEntry> _training = new();
    private readonly List<CertificateEntry> _certificates = new();
    private readonly List<SocialLink> _socialLinks = new();
    private readonly Dictionary<string, SectionState> _sectionStatus = new();

    private Profile()
    {
    }

    public Guid OwnerAccountId { get; private set; }
    public ProfileStatus Status { get; private set; }

    // Level 1
    public FullName FullName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public MobileNumber MobileNumber { get; private set; } = null!;
    public Gender Gender { get; private set; }

    // Level 2
    public IReadOnlyList<EducationEntry> Education => _education;
    public IReadOnlyList<ExperienceEntry> Experience => _experience;
    public IReadOnlyList<SkillEntry> Skills => _skills;
    public IReadOnlyList<TrainingEntry> Training => _training;
    public IReadOnlyList<CertificateEntry> Certificates => _certificates;
    public SalaryRange? SalaryExpectation { get; private set; }
    public Address? Address { get; private set; }
    public decimal? YearsOfExperience { get; private set; }

    // Level 3
    public IReadOnlyList<SocialLink> SocialLinks => _socialLinks;
    public string? Statement { get; private set; }
    public string? Bio { get; private set; }

    public IReadOnlyDictionary<string, SectionState> SectionStatus => _sectionStatus;
    public int CompletionPercent { get; private set; }

    /// <summary>INV-01 account must be active and JobSeeker (checked by the caller from the KnownAccounts replica); INV-02 Level 1 required fields.</summary>
    public static Profile Create(Guid id, Guid ownerAccountId, FullName fullName, Email email, MobileNumber mobileNumber, Gender gender,
        bool accountActive, Guid actorId, DateTime nowUtc)
    {
        Check(new BusinessRule(RuleCodes.AccountNotActive, "The account must be an active job seeker before a profile can be created.",
            !accountActive, ErrorCodes.AccountNotActive, BusinessRuleKind.BusinessRule));

        var profile = new Profile
        {
            Id = id,
            OwnerAccountId = ownerAccountId,
            Status = ProfileStatus.Active,
            FullName = fullName,
            Email = email,
            MobileNumber = mobileNumber,
            Gender = gender
        };
        profile.RecomputeCompletion();
        profile.Raise(new ProfileCreatedDomainEvent(id, ownerAccountId, actorId, nowUtc));
        return profile;
    }

    public void EnsureOwnedBy(Actor actor) => Check(Rules.NotOwner(actor, OwnerAccountId));

    public void UpdateLevel1(Actor actor, FullName fullName, Email email, MobileNumber mobileNumber, Gender gender, Guid actorId, DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        FullName = fullName;
        Email = email;
        MobileNumber = mobileNumber;
        Gender = gender;
        RaiseUpdated(ProfileSections.Level1, actorId, nowUtc);
    }

    private void EnsureLevel1Complete()
    {
        // INV-03: Level 2/3 sections only accepted after Level 1 is complete. Level 1 fields are non-null by construction, so this
        // only fails for a profile created before Level 1 was fully captured (defensive; the Application validator already enforces it).
        Check(new BusinessRule(RuleCodes.Level1Incomplete, "Complete Level 1 before adding this section.",
            _sectionStatus.GetValueOrDefault(ProfileSections.Level1) != SectionState.Complete, ErrorCodes.Level1Incomplete, BusinessRuleKind.BusinessRule));
    }

    public void UpdateEducation(Actor actor, IReadOnlyList<(string Degree, string Institution, DateTime? From, DateTime? To)> entries, Guid actorId,
        DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        EnsureLevel1Complete();
        _education.Clear();
        _education.AddRange(entries.Select(e => new EducationEntry(Guid.NewGuid(), e.Degree, e.Institution, e.From, e.To, DataSource.User)));
        RaiseUpdated(ProfileSections.Education, actorId, nowUtc);
    }

    public void UpdateExperience(Actor actor, IReadOnlyList<(string Company, string Role, DateTime? From, DateTime? To)> entries, Guid actorId,
        DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        EnsureLevel1Complete();
        _experience.RemoveAll(e => e.Source == DataSource.User);
        _experience.AddRange(entries.Select(e => new ExperienceEntry(Guid.NewGuid(), e.Company, e.Role, e.From, e.To, DataSource.User)));
        RaiseUpdated(ProfileSections.Experience, actorId, nowUtc);
    }

    public void UpdateSkills(Actor actor, IReadOnlyList<(string Name, SkillKind Kind, SkillClass Class)> entries, Guid actorId, DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        EnsureLevel1Complete();
        _skills.RemoveAll(s => s.Source == DataSource.User);
        _skills.AddRange(entries.Select(e => new SkillEntry(Guid.NewGuid(), e.Name, e.Kind, e.Class, DataSource.User)));
        RaiseUpdated(ProfileSections.Skills, actorId, nowUtc);
    }

    public void UpdateTraining(Actor actor, IReadOnlyList<(string Name, string? Provider, DateTime? CompletedOn)> entries, Guid actorId, DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        EnsureLevel1Complete();
        _training.Clear();
        _training.AddRange(entries.Select(e => new TrainingEntry(Guid.NewGuid(), e.Name, e.Provider, e.CompletedOn, DataSource.User)));
        RaiseUpdated(ProfileSections.Training, actorId, nowUtc);
    }

    public void UpdateCertificates(Actor actor, IReadOnlyList<(string Name, string? Issuer, DateTime? IssuedOn)> entries, Guid actorId, DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        EnsureLevel1Complete();
        _certificates.Clear();
        _certificates.AddRange(entries.Select(e => new CertificateEntry(Guid.NewGuid(), e.Name, e.Issuer, e.IssuedOn, DataSource.User)));
        RaiseUpdated(ProfileSections.Certificates, actorId, nowUtc);
    }

    public void UpdateSalaryExpectationAndAddress(Actor actor, SalaryRange? salary, Address? address, decimal? yearsOfExperience, Guid actorId, DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        EnsureLevel1Complete();
        SalaryExpectation = salary;
        Address = address;
        YearsOfExperience = yearsOfExperience ?? YearsOfExperience;
        RaiseUpdated(ProfileSections.Experience, actorId, nowUtc);
    }

    public void UpdateLevel3(Actor actor, IReadOnlyList<(string Network, string Url)> socialLinks, string? statement, string? bio, Guid actorId, DateTime nowUtc)
    {
        EnsureOwnedBy(actor);
        EnsureLevel1Complete();
        _socialLinks.Clear();
        _socialLinks.AddRange(socialLinks.Select(l => new SocialLink(l.Network, l.Url)));
        Statement = statement;
        Bio = bio;
        RaiseUpdated(ProfileSections.Level3, actorId, nowUtc);
    }

    /// <summary>
    /// INV-05 (checked by the caller: a resume must exist) merges AI-extracted data. Never overwrites a user-entered value; idempotent
    /// per resumeParsedDataId is enforced by the Application layer (profile.ProcessedParsedData) before this is called.
    /// </summary>
    public void ApplyExtractedData(IReadOnlyList<string> skills, IReadOnlyList<string> jobTitles, decimal? yearsOfExperience, Guid actorId, DateTime nowUtc)
    {
        var existingSkillNames = _skills.Select(s => s.NormalizedName).ToHashSet();
        foreach (var skill in skills)
        {
            var normalized = skill.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || !existingSkillNames.Add(normalized))
            {
                continue;
            }

            _skills.Add(new SkillEntry(Guid.NewGuid(), skill.Trim(), SkillKind.Primary, SkillClass.Hard, DataSource.ResumeParsing));
        }

        var existingRoles = _experience.Select(e => e.Role.Trim().ToLowerInvariant()).ToHashSet();
        foreach (var title in jobTitles)
        {
            var normalized = title.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || !existingRoles.Add(normalized))
            {
                continue;
            }

            _experience.Add(new ExperienceEntry(Guid.NewGuid(), string.Empty, title.Trim(), null, null, DataSource.ResumeParsing));
        }

        YearsOfExperience ??= yearsOfExperience;
        RaiseUpdated(ProfileSections.ExtractedData, actorId, nowUtc);
    }

    /// <summary>Applies the job seeker's *reviewed* corrections (ParsedProfileDataUpdated): replaces only the resume-parsed skill set.</summary>
    public void ApplyReviewedSkills(IReadOnlyList<string> reviewedSkills, Guid actorId, DateTime nowUtc)
    {
        _skills.RemoveAll(s => s.Source == DataSource.ResumeParsing);
        var existing = _skills.Select(s => s.NormalizedName).ToHashSet();
        foreach (var skill in reviewedSkills)
        {
            var normalized = skill.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || !existing.Add(normalized))
            {
                continue;
            }

            _skills.Add(new SkillEntry(Guid.NewGuid(), skill.Trim(), SkillKind.Primary, SkillClass.Hard, DataSource.ResumeParsing));
        }

        RaiseUpdated(ProfileSections.ExtractedData, actorId, nowUtc);
    }

    public void Deactivate(Guid actorId, DateTime nowUtc)
    {
        if (Status == ProfileStatus.Deactivated)
        {
            return;
        }

        Status = ProfileStatus.Deactivated;
        RaiseUpdated("Status", actorId, nowUtc);
    }

    /// <summary>US-3.1.1-05: percentage plus a nudge of missing sections; empty hint when complete (AC-02).</summary>
    public (int Percent, IReadOnlyList<string> MissingSections) ComputeCompletionRecommendation() =>
        (CompletionPercent, _sectionStatus.Where(s => s.Value != SectionState.Complete).Select(s => s.Key).ToList());

    private void RaiseUpdated(string section, Guid actorId, DateTime nowUtc)
    {
        RecomputeCompletion();
        Raise(new ProfileUpdatedDomainEvent(Id, "Active", "Active", actorId, new[] { section }, CompletionPercent, nowUtc));
    }

    private void RecomputeCompletion()
    {
        _sectionStatus[ProfileSections.Level1] = SectionState.Complete; // Level 1 fields are mandatory at creation time.
        _sectionStatus[ProfileSections.Education] = _education.Count > 0 ? SectionState.Complete : SectionState.NotStarted;
        _sectionStatus[ProfileSections.Experience] = _experience.Count > 0 ? SectionState.Complete : SectionState.NotStarted;
        _sectionStatus[ProfileSections.Skills] = _skills.Count > 0 ? SectionState.Complete : SectionState.NotStarted;
        _sectionStatus[ProfileSections.Training] = _training.Count > 0 || _certificates.Count > 0 ? SectionState.Complete : SectionState.NotStarted;
        _sectionStatus[ProfileSections.Level3] = _socialLinks.Count > 0 || !string.IsNullOrWhiteSpace(Statement) || !string.IsNullOrWhiteSpace(Bio)
            ? SectionState.Complete
            : SectionState.NotStarted;

        var percent = 0;
        percent += CompletionWeights.Level1;
        percent += _sectionStatus[ProfileSections.Education] == SectionState.Complete ? CompletionWeights.Education : 0;
        percent += _sectionStatus[ProfileSections.Experience] == SectionState.Complete ? CompletionWeights.Experience : 0;
        percent += _sectionStatus[ProfileSections.Skills] == SectionState.Complete ? CompletionWeights.Skills : 0;
        percent += _sectionStatus[ProfileSections.Training] == SectionState.Complete ? CompletionWeights.TrainingAndCertificates : 0;
        percent += _sectionStatus[ProfileSections.Level3] == SectionState.Complete ? CompletionWeights.Level3 : 0;
        CompletionPercent = percent;
    }
}
