using FluentValidation;
using JobPlatform.CandidateSourcing.Application.DTOs.Search;
using JobPlatform.CandidateSourcing.Application.Queries.Search;
using JobPlatform.CandidateSourcing.Domain.Common;

namespace JobPlatform.CandidateSourcing.Application.Validators.Search;

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
