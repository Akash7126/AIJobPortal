using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.Feedback;
using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.Validators.Feedback;

public sealed class SubmitHelpFeedbackValidator : AbstractValidator<SubmitHelpFeedbackCommand>
{
    public SubmitHelpFeedbackValidator()
    {
        RuleFor(c => c.Rating).IsInEnum().WithErrorCode("VAL.Rating.Invalid");
        RuleFor(c => c.Comment).MaximumLength(HelpFeedback.MaxCommentLength).WithErrorCode("VAL.Comment.TooLong");
    }
}
