using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.Help;
using JobPlatform.HelpContent.Application.Validators.Common;

namespace JobPlatform.HelpContent.Application.Validators.Help;

public sealed class UpdateHelpContentValidator : AbstractValidator<UpdateHelpContentCommand>
{
    public UpdateHelpContentValidator() => Include(new TitleBodyRules<UpdateHelpContentCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
}
