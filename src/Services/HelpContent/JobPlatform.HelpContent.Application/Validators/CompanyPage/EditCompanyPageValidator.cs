using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.CompanyPage;
using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.Validators.CompanyPage;

public sealed class EditCompanyPageValidator : AbstractValidator<EditCompanyPageCommand>
{
    public EditCompanyPageValidator()
    {
        RuleFor(c => c.BackgroundAr).MaximumLength(CompanyProfilePage.MaxBackgroundLength).WithErrorCode("VAL.BackgroundAr.TooLong");
        RuleFor(c => c.BackgroundEn).MaximumLength(CompanyProfilePage.MaxBackgroundLength).WithErrorCode("VAL.BackgroundEn.TooLong");
        RuleFor(c => c.Highlights).Must(h => h.Count <= CompanyProfilePage.MaxHighlights).WithErrorCode("VAL.Highlights.TooMany");
    }
}
