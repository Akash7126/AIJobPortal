using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.CandidateSourcing.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): the candidate database search and the talent pool listing, over the local projection and entries.</summary>
public interface ICandidateSourcingReadStore
{
    Task<IReadOnlyList<TalentPoolEntryView>> ListTalentPoolAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<PagedResult<CandidateSearchResultItemView>> SearchCandidatesAsync(CandidateSearchCriteria criteria, PageRequest page, CancellationToken ct = default);
}
