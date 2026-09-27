using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.EmployerOnboarding.Domain;

public enum RegistrationStatus
{
    Pending,
    Approved
}

public enum CompanySize
{
    Micro,
    Small,
    Medium,
    Large
}

/// <summary>Company identity copied at Level-2 submission time (Q-01: AccountApproved carries actorType only, not company identity - the employer
/// supplies these fields themselves when they submit Level 2, since BC-03/BC-05 do not currently share a richer contract for it).</summary>
public sealed class CompanyIdentity : ValueObject
{
    public CompanyIdentity(string name, string companyId, string registrationNumber)
    {
        Name = name;
        CompanyId = companyId;
        RegistrationNumber = registrationNumber;
    }

    public string Name { get; }
    public string CompanyId { get; }
    public string RegistrationNumber { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return CompanyId;
        yield return RegistrationNumber;
    }
}

public sealed class Address : ValueObject
{
    public Address(string governorate, string city, string? street)
    {
        Governorate = governorate;
        City = city;
        Street = street;
    }

    public string Governorate { get; }
    public string City { get; }
    public string? Street { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Governorate;
        yield return City;
        yield return Street;
    }
}

/// <summary>Level-2 company details (handover section 3.1). Present once submitted; null fields mean "not yet submitted".</summary>
public sealed class Level2Details : ValueObject
{
    public Level2Details(string website, string industry, CompanySize size, Address address, string description)
    {
        Website = website;
        Industry = industry;
        Size = size;
        Address = address;
        Description = description;
    }

    public string Website { get; }
    public string Industry { get; }
    public CompanySize Size { get; }
    public Address Address { get; }
    public string Description { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Website;
        yield return Industry;
        yield return Size;
        yield return Address;
        yield return Description;
    }
}

/// <summary>
/// AGG-13: the employer's application for admission. Opened (Pending) when BC-03 approves the account (AccountApproved, actorType Employer);
/// moves to Approved once an administrator reviews the Level-2 profile the employer submitted. No reject path exists in the source stories (Q-02).
/// </summary>
public sealed class EmployerRegistration : AggregateRoot<Guid>
{
    private EmployerRegistration()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public CompanyIdentity? CompanyIdentity { get; private set; }

    public Level2Details? Level2 { get; private set; }

    public RegistrationStatus Status { get; private set; }

    public DateTime OpenedAtUtc { get; private set; }

    public Guid? ApprovedBy { get; private set; }

    public DateTime? ApprovedAtUtc { get; private set; }

    /// <summary>Opens a Pending registration for a newly-approved employer account. Called once per account (INV: at most one registration per employer).</summary>
    public static EmployerRegistration OpenFor(Guid id, Guid employerAccountId, DateTime nowUtc)
    {
        var registration = new EmployerRegistration
        {
            Id = id,
            EmployerAccountId = employerAccountId,
            Status = RegistrationStatus.Pending,
            OpenedAtUtc = nowUtc
        };
        return registration;
    }

    /// <summary>Employer supplies (or amends) Level-2 details and, until BC-03 exposes it, the company identity Q-01 originally captured at Level 1.</summary>
    public void SubmitLevel2(CompanyIdentity identity, Level2Details details, Actor actor)
    {
        Check(Rules.OwnerOnly(actor, EmployerAccountId, RuleCodes.RegistrationOwnerOnly, ErrorCodes.EmployerForbidden));
        Check(new BusinessRule(RuleCodes.RegistrationNotPending, "Level-2 details can only be edited while the registration is pending.",
            Status != RegistrationStatus.Pending, ErrorCodes.RegistrationNotPending, BusinessRuleKind.Conflict));

        CompanyIdentity = identity;
        Level2 = details;
    }

    /// <summary>INV-02 already approved; INV-03 administrators only; INV-04 approval requires submitted Level-2 details.</summary>
    public void Approve(Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor));
        Check(new BusinessRule(RuleCodes.RegistrationAlreadyApproved, "This registration has already been approved.",
            Status == RegistrationStatus.Approved, ErrorCodes.RegistrationAlreadyApproved, BusinessRuleKind.Conflict));
        Check(new BusinessRule(RuleCodes.RegistrationProfileNotSubmitted, "The employer has not submitted their Level-2 profile information yet.",
            Level2 is null, ErrorCodes.RegistrationProfileNotSubmitted, BusinessRuleKind.BusinessRule));

        Status = RegistrationStatus.Approved;
        ApprovedBy = actor.Id;
        ApprovedAtUtc = nowUtc;
        Raise(new EmployerRegistrationApprovedDomainEvent(Id, EmployerAccountId, actor.Id, nowUtc));
    }
}
