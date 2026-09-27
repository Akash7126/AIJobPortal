namespace JobPlatform.EmployerOnboarding.Domain;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface IEmployerRegistrationRepository
{
    Task<EmployerRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EmployerRegistration?> GetByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(EmployerRegistration registration);
}

public interface ICompanyMediaRepository
{
    Task<CompanyMediaAndDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CompanyMediaAndDocument?> GetByHashAsync(Guid employerAccountId, string sha256, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyMediaAndDocument>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(CompanyMediaAndDocument media);
}

public interface IEmployerStandingRepository
{
    Task<EmployerStanding?> GetAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(EmployerStanding standing);
}

public interface IKnownAccountRepository
{
    Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default);

    void Add(KnownAccount account);
}
