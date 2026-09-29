using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.News;

namespace JobPlatform.HelpContent.Application.Validators.News;

public sealed class UpdateContentCategorizationValidator : AbstractValidator<UpdateContentCategorizationCommand>
{
    public UpdateContentCategorizationValidator()
    {
        RuleFor(c => c.Tags).Must(t => t.Count <= 20).WithErrorCode("VAL.Tags.TooMany");
        RuleForEach(c => c.Tags).MaximumLength(50).WithErrorCode("VAL.Tag.TooLong");
    }
}
