using FluentValidation;
using JobPlatform.HelpContent.Application.Commands.News;
using JobPlatform.HelpContent.Domain;

namespace JobPlatform.HelpContent.Application.Validators.News;

public sealed class AddNewsMediaValidator : AbstractValidator<AddNewsMediaCommand>
{
    public AddNewsMediaValidator()
    {
        RuleFor(c => c.Type).IsInEnum().WithErrorCode("VAL.Type.Invalid");
        RuleFor(c => c.FileName).NotEmpty().MaximumLength(260).WithErrorCode("VAL.FileName.Required");
        RuleFor(c => c.SizeBytes).GreaterThan(0).LessThanOrEqualTo(NewsArticle.MaxMediaSizeBytes).WithErrorCode("VAL.SizeBytes.TooLarge");
        RuleFor(c => c.AltText).NotEmpty().When(c => c.Type == NewsMediaType.Image).WithErrorCode("VAL.AltText.RequiredForImages");
    }
}
