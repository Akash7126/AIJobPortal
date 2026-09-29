using FluentValidation;
using JobPlatform.Reporting.Application.Commands.Activity;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.Activity;

public sealed class SetActivityRetentionPolicyValidator : AbstractValidator<SetActivityRetentionPolicyCommand>
{
    public SetActivityRetentionPolicyValidator() =>
        RuleFor(x => x.Months).InclusiveBetween(1, ActivityLogRetentionPolicy.MaxMonths).WithErrorCode("VAL.Months.OutOfRange");
}
