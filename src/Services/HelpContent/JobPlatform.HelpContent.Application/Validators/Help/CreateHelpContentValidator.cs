using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.Validators.Common;

namespace JobPlatform.HelpContent.Application.Validators.Help;

public sealed class CreateHelpContentValidator : AbstractValidator<CreateHelpContentCommand>
{
    public CreateHelpContentValidator()
    {
        Include(new TitleBodyRules<CreateHelpContentCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("VAL.Kind.Invalid");
    }
}
