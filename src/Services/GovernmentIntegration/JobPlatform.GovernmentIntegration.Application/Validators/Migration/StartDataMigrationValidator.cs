using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.Migration;

namespace JobPlatform.GovernmentIntegration.Application.Validators.Migration;

public sealed class StartDataMigrationValidator : AbstractValidator<StartDataMigrationCommand>
{
    public StartDataMigrationValidator() =>
        RuleFor(c => c.Phases).NotEmpty().WithErrorCode("VAL.Phases.Required");
}
