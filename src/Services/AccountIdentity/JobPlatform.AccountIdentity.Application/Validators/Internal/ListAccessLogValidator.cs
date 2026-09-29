using FluentValidation;
using JobPlatform.AccountIdentity.Application.Queries.Internal;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Internal;

public sealed class ListAccessLogValidator : AbstractValidator<ListAccessLogQuery>
{
    public ListAccessLogValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x).Must(x => x.FromUtc is null || x.ToUtc is null || x.FromUtc <= x.ToUtc).OverridePropertyName("Range").WithErrorCode("VAL.Range.Invalid");
    }
}
