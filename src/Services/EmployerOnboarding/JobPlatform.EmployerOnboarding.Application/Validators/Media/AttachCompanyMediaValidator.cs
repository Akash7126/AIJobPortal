using FluentValidation;
using JobPlatform.EmployerOnboarding.Application.Commands.Media;
using JobPlatform.EmployerOnboarding.Domain;

namespace JobPlatform.EmployerOnboarding.Application.Validators.Media;

public sealed class AttachCompanyMediaValidator : AbstractValidator<AttachCompanyMediaCommand>
{
    public AttachCompanyMediaValidator()
    {
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("VAL.Kind.Invalid");
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260).WithErrorCode("VAL.FileName.Required");
        RuleFor(c => c.ContentType).NotEmpty().WithErrorCode("VAL.ContentType.Required");
        RuleFor(c => c.SizeBytes).GreaterThan(0).LessThanOrEqualTo(CompanyMediaAndDocument.MaxSizeBytes).WithErrorCode("VAL.SizeBytes.TooLarge");
    }
}
