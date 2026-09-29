using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.Validators.Common;

namespace JobPlatform.HelpContent.Application.Validators.News;

public sealed class CreateNewsArticleValidator : AbstractValidator<CreateNewsArticleCommand>
{
    public CreateNewsArticleValidator()
    {
        Include(new TitleBodyRules<CreateNewsArticleCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("VAL.Kind.Invalid");
    }
}
