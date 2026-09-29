using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Profile;

public sealed class UpdateCertificatesValidator : AbstractValidator<UpdateCertificatesCommand>
{
    public UpdateCertificatesValidator() =>
        RuleForEach(c => c.Entries).ChildRules(e => e.RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Certificate.Invalid"));
}
