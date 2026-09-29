using FluentValidation;
using JobPlatform.AiMatching.Application.Commands.Parsing;

namespace JobPlatform.AiMatching.Application.Validators.Parsing;

public sealed class ParseResumeValidator : AbstractValidator<ParseResumeCommand>
{
    public ParseResumeValidator()
    {
        RuleFor(x => x.ResumeId).NotEmpty().WithErrorCode("VAL.ResumeId.Required");
        RuleFor(x => x.ProfileId).NotEmpty().WithErrorCode("VAL.ProfileId.Required");
        RuleFor(x => x.Sha256).NotEmpty().MaximumLength(64).WithErrorCode("VAL.Sha256.Required");
        RuleFor(x => x.SizeBytes).GreaterThanOrEqualTo(0).WithErrorCode("VAL.SizeBytes.Invalid");
        // Format and size limits (PDF/DOCX/TXT, 10 MB) are applied by the handler as an INV-08 Failed result, not as a request error.
    }
}
