using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Validators.Migration;

public sealed class RollbackMigrationRunValidator : AbstractValidator<RollbackMigrationRunCommand>
{
    public RollbackMigrationRunValidator()
    {
        RuleFor(c => c.MigrationRunId).NotEmpty().WithErrorCode("VAL.MigrationRunId.Required");
        RuleFor(c => c.Reason).NotEmpty().WithErrorCode("VAL.Reason.Required");
    }
}
