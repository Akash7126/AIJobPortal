using FluentValidation;
using JobPlatform.AiMatching.Application.Commands.Shortlists;
using Microsoft.Extensions.Options;

namespace JobPlatform.AiMatching.Application.Validators.Shortlists;

public sealed class ComputeCandidateShortlistValidator : AbstractValidator<ComputeCandidateShortlistCommand>
{
    public ComputeCandidateShortlistValidator(IOptions<MatchingOptions> options)
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        When(x => x.Size.HasValue, () => RuleFor(x => x.Size!.Value).InclusiveBetween(1, options.Value.MaxShortlistSize).OverridePropertyName("Size")
            .WithErrorCode("VAL.Size.OutOfRange"));
    }
}
