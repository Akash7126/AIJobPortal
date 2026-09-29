using FluentValidation;
using JobPlatform.JobPosting.Application.Queries.Postings;
using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.JobPosting.Application.Validators.Postings;

public sealed class SearchJobPostingsValidator : AbstractValidator<SearchJobPostingsQuery>
{
    private static readonly string[] AllowedSorts = { "relevance", "newest", "deadline" };

    public SearchJobPostingsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");
        RuleFor(q => q.Keyword).MaximumLength(100).WithErrorCode("VAL.Keyword.TooLong");
        RuleFor(q => q).Must(q => q.SalaryMin is null || q.SalaryMax is null || q.SalaryMin <= q.SalaryMax)
            .WithErrorCode(ErrorCodes.SearchInvalidField).WithName("salary");
        RuleFor(q => q.Sort).Must(s => s is null || AllowedSorts.Contains(s)).WithErrorCode("VAL.Sort.Invalid");
        RuleFor(q => q.ContractType).IsEnumName(typeof(ContractType), false).WithErrorCode("VAL.ContractType.Invalid").When(q => q.ContractType is not null);
    }
}
