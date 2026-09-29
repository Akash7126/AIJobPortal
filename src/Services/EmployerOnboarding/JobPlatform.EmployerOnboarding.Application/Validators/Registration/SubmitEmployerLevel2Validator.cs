using FluentValidation;
using JobPlatform.EmployerOnboarding.Application.Commands.Registration;

namespace JobPlatform.EmployerOnboarding.Application.Validators.Registration;

public sealed class SubmitEmployerLevel2Validator : AbstractValidator<SubmitEmployerLevel2Command>
{
    public SubmitEmployerLevel2Validator()
    {
        RuleFor(c => c.CompanyName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.CompanyName.Required");
        RuleFor(c => c.CompanyId).NotEmpty().MaximumLength(100).WithErrorCode("VAL.CompanyId.Required");
        RuleFor(c => c.RegistrationNumber).NotEmpty().MaximumLength(100).WithErrorCode("VAL.RegistrationNumber.Required");
        RuleFor(c => c.Website).NotEmpty().Must(BeAnAbsoluteHttpUrl).WithErrorCode("VAL.Website.Invalid");
        RuleFor(c => c.Industry).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Industry.Required");
        RuleFor(c => c.Size).IsInEnum().WithErrorCode("VAL.Size.Invalid");
        RuleFor(c => c.Governorate).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Governorate.Required");
        RuleFor(c => c.City).NotEmpty().MaximumLength(100).WithErrorCode("VAL.City.Required");
        RuleFor(c => c.Description).NotEmpty().MaximumLength(2000).WithErrorCode("VAL.Description.TooLong");
    }

    private static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
