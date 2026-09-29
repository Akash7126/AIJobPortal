using FluentValidation;
using JobPlatform.Reporting.Application.Queries.ODataFeed;

namespace JobPlatform.Reporting.Application.Validators.ODataFeed;

public sealed class GetODataViewValidator : AbstractValidator<GetODataViewQuery>
{
    public GetODataViewValidator()
    {
        RuleFor(x => x.View).Must(v => GetODataViewQuery.Views.ContainsKey(v)).WithErrorCode("VAL.View.Unknown");
        RuleFor(x => x.Top).InclusiveBetween(1, 10_000).WithErrorCode("VAL.Top.OutOfRange");
    }
}
