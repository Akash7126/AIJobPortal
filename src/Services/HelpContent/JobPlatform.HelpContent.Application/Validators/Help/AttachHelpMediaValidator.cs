using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.Help;

namespace JobPlatform.HelpContent.Application.Validators.Help;

public sealed class AttachHelpMediaValidator : AbstractValidator<AttachHelpMediaCommand>
{
    public AttachHelpMediaValidator()
    {
        RuleFor(c => c.Type).IsInEnum().WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(c.CaptionsRef) || !string.IsNullOrWhiteSpace(c.TextAlternative))
            .WithErrorCode("VAL.Captions.Required").OverridePropertyName(nameof(AttachHelpMediaCommand.CaptionsRef));
    }
}
