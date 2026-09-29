using FluentValidation;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    private static readonly string[] Standings = { "Pending", "Active", "Banned", "Deactivated" };

    public ListAccountsValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        When(x => !string.IsNullOrEmpty(x.Standing), () =>
            RuleFor(x => x.Standing!).Must(s => Standings.Contains(s)).WithErrorCode("VAL.Standing.Invalid"));
        When(x => !string.IsNullOrEmpty(x.Search), () =>
            RuleFor(x => x.Search!).MaximumLength(100).WithErrorCode("VAL.Search.TooLong"));
    }
}
