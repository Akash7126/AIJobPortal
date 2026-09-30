using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.CandidateSourcing.Application.Interfaces;
using JobPlatform.CandidateSourcing.Application.Queries.Search;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.Search;

internal sealed class SearchCandidateDatabaseHandler : IQueryHandler<SearchCandidateDatabaseQuery, PagedResult<CandidateSearchResultItemView>>
{
    private readonly ICandidateSourcingReadStore _store;
    private readonly IVerifiedEmployerRepository _verifiedEmployers;
    private readonly ICurrentUser _user;

    public SearchCandidateDatabaseHandler(ICandidateSourcingReadStore store, IVerifiedEmployerRepository verifiedEmployers, ICurrentUser user)
    {
        _store = store;
        _verifiedEmployers = verifiedEmployers;
        _user = user;
    }

    public async Task<Result<PagedResult<CandidateSearchResultItemView>>> Handle(SearchCandidateDatabaseQuery request, CancellationToken ct)
    {
        var employerId = ActorFactory.From(_user).Id;
        if (!await _verifiedEmployers.IsVerifiedAsync(employerId, ct))
        {
            return Error.Forbidden(ErrorCodes.Forbidden, "Only verified employers may search the candidate database.");
        }

        var page = new PageRequest(request.Page, request.PageSize);
        return await _store.SearchCandidatesAsync(request.Criteria, page, ct);
    }
}
