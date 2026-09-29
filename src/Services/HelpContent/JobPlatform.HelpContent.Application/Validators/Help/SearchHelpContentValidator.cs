using FluentValidation;
using JobPlatform.HelpContent.Application.Queries.Help;

namespace JobPlatform.HelpContent.Application.Validators.Help;

public sealed class SearchHelpContentValidator : AbstractValidator<SearchHelpContentQuery>
{
    public SearchHelpContentValidator()
    {
        RuleFor(c => c.Keyword).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Keyword.Required");
        RuleFor(c => c.PageSize).LessThanOrEqualTo(50).WithErrorCode("VAL.PageSize.TooLarge");
    }
}
