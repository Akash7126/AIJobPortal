using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Commands.Entities;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.PlatformAdministration.Application.Validators.Entities;

public sealed class CreatePlatformEntityRecordValidator : AbstractValidator<CreatePlatformEntityRecordCommand>
{
    private const int MaxFields = 20;
    private const int MaxValueLength = 200;

    public CreatePlatformEntityRecordValidator()
    {
        RuleFor(c => c.EntityType).IsInEnum().WithErrorCode("VAL.EntityType.Invalid");
        RuleFor(c => c.Core).NotNull().WithErrorCode("VAL.Core.Required");
        RuleFor(c => c.Core!).Must(core => core.Count <= MaxFields).WithErrorCode("VAL.Core.TooManyFields").When(c => c.Core is not null);
        RuleFor(c => c.Core!).Must(core => core.Values.All(v => v is null || v.Length <= MaxValueLength))
            .WithErrorCode("VAL.Core.ValueTooLong").When(c => c.Core is not null);

        // Required fields mirror the self-service counterpart (INV-01, shared specification in the domain).
        RuleFor(c => c).Custom((command, context) =>
        {
            if (command.Core is null || !Enum.IsDefined(command.EntityType))
            {
                return;
            }

            var core = EntityCore.From(command.Core);
            foreach (var field in EntityCoreSpecification.MissingFields(command.EntityType, core))
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure($"Core.{field}", "required") { ErrorCode = $"VAL.{Capitalise(field)}.Required" });
            }

            if (core.Get(EntityCoreSpecification.Mobile) is { } mobile && !MobileNumber.TryCreate(mobile, out _))
            {
                Fail(context, EntityCoreSpecification.Mobile, "VAL.MobileNumber.Invalid");
            }

            if (core.Get(EntityCoreSpecification.Email) is { } email && !Email.TryCreate(email, out _))
            {
                Fail(context, EntityCoreSpecification.Email, "VAL.Email.Invalid");
            }
        });
    }

    private static void Fail(FluentValidation.ValidationContext<CreatePlatformEntityRecordCommand> context, string field, string code)
    {
        context.AddFailure(new FluentValidation.Results.ValidationFailure($"Core.{field}", "invalid") { ErrorCode = code });
    }

    private static string Capitalise(string field) => char.ToUpperInvariant(field[0]) + field[1..];
}
