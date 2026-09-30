namespace JobPlatform.HelpContent.Application.Interfaces;

/// <summary>Company info and verification standing from BC-05's internal API (handover section 6.2).</summary>
public interface ICompanyDirectoryProvider
{
    Task<CompanyDirectoryEntry?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default);
}
