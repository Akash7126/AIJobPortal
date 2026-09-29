using FluentValidation;
using JobPlatform.JobPosting.Application.Commands.Favorites;

namespace JobPlatform.JobPosting.Application.Validators.Favorites;

public sealed class ToggleFavoriteJobValidator : AbstractValidator<ToggleFavoriteJobCommand>
{
    public ToggleFavoriteJobValidator() => RuleFor(c => c.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
}
