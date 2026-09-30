using JobPlatform.HelpContent.Application.DTOs.CompanyPage;

namespace JobPlatform.HelpContent.Application.Interfaces;

/// <summary>Q-05 (resolved: BC-09 now exposes GET /internal/v1/employers/{id}/open-postings): current job openings for the company page,
/// degrading to an empty list when BC-09 is unavailable (handover section 6.2).</summary>
public interface IOpenPostingsProvider
{
    Task<(IReadOnlyList<OpenPostingView> Items, bool Degraded)> GetOpenPostingsAsync(Guid employerAccountId, CancellationToken ct = default);
}
