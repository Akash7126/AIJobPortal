using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Privacy;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Privacy;

public sealed class RequestAccountDeletionValidator : AbstractValidator<RequestAccountDeletionCommand>
{
    public RequestAccountDeletionValidator() => RuleFor(c => c.Confirm).Equal(true).WithErrorCode("VAL.Confirm.Required");
}
