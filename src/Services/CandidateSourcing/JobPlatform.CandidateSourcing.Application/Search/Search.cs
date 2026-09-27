using FluentValidation;
using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Search;

/// <summary>US-3.3.3-04: full-text-ish filter search over the local candidate projection. Verified employers only (CS.Search.NOT_VERIFIED).</summary>
public sealed record SearchCandidateDatabaseQuery(CandidateSearchCriteria Criteria, int Page, int PageSize) : EmployerQuery<PagedResult<CandidateSearchResultItemView>>;

public sealed class SearchCandidateDatabaseValidator : AbstractValidator<SearchCandidateDatabaseQuery>
{
    public SearchCandidateDatabaseValidator()
    {
        RuleFor(q => q.PageSize).LessThanOrEqualTo(50).WithErrorCode("VAL.PageSize.TooLarge");
        RuleFor(q => q.Criteria).Must(NotContradictory).WithErrorCode(RuleCodes.SearchInvalidFilters).WithName("criteria");
    }

    /// <summary>GAP-001: the only well-defined contradiction with this filter set is an inverted salary range.</summary>
    private static bool NotContradictory(CandidateSearchCriteria criteria) =>
        criteria.SalaryMin is null || criteria.SalaryMax is null || criteria.SalaryMin <= criteria.SalaryMax;
}

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

/// <summary>US-3.3.3-05: the privacy-filtered view of one candidate. Pass-through to BC-04's own privacy-aware endpoint (it is the source of truth
/// for what a candidate has disclosed) - this BC never re-derives it from the projection replica.</summary>
public sealed record GetCandidateViewQuery(Guid CandidateProfileId) : EmployerQuery<CandidateViewDto>;

internal sealed class GetCandidateViewHandler : IQueryHandler<GetCandidateViewQuery, CandidateViewDto>
{
    private readonly IJobSeekerProfileApi _profiles;

    public GetCandidateViewHandler(IJobSeekerProfileApi profiles) => _profiles = profiles;

    public async Task<Result<CandidateViewDto>> Handle(GetCandidateViewQuery request, CancellationToken ct)
    {
        var view = await _profiles.GetCandidateViewAsync(request.CandidateProfileId, ct);
        if (view is null || view.Deactivated || (view.Visibility == "Private" && !view.EmployerVisibilityOptIn))
        {
            return Error.NotFound(ErrorCodes.NotFound, "The candidate was not found or is not visible.");
        }

        return view;
    }
}
