using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.Validators.JobDataFlows;

public sealed class SyncJobPostAttributionValidator : AbstractValidator<SyncJobPostAttributionCommand>
{
    public SyncJobPostAttributionValidator()
    {
        RuleFor(c => c.PlatformJobId).NotEmpty().WithErrorCode("VAL.PlatformJobId.Required");
        RuleFor(c => c.Operation).Must(o => Enum.TryParse<AttributionOperation>(o, true, out _)).WithErrorCode("VAL.Operation.Invalid");
        When(c => Enum.TryParse<AttributionOperation>(c.Operation, true, out var op) && op == AttributionOperation.ExtendDeadline, () =>
            RuleFor(c => c.Deadline).NotNull().WithErrorCode("VAL.Deadline.Required"));
        When(c => Enum.TryParse<AttributionOperation>(c.Operation, true, out var op) && op == AttributionOperation.EditDescription, () =>
        {
            RuleFor(c => c.Description).NotEmpty().WithErrorCode("VAL.Description.Required");
            RuleFor(c => c.Description).MaximumLength(10_000).WithErrorCode("VAL.Description.TooLong");
        });
    }
}
