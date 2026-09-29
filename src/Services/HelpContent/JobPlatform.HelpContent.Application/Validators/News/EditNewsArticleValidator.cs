using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Application.Validators.Common;

namespace JobPlatform.HelpContent.Application.Validators.News;

public sealed class EditNewsArticleValidator : AbstractValidator<EditNewsArticleCommand>
{
    public EditNewsArticleValidator() => Include(new TitleBodyRules<EditNewsArticleCommand>(c => c.TitleAr, c => c.TitleEn, c => c.BodyAr, c => c.BodyEn));
}
