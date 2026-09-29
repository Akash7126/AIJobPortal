using FluentValidation;
using JobPlatform.AiMatching.Application.Queries.Shortlists;

namespace JobPlatform.AiMatching.Application.Validators.Shortlists;

public sealed class GetCandidateShortlistValidator : AbstractValidator<GetCandidateShortlistQuery>
{
    public GetCandidateShortlistValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(x => x.ShortlistId).NotEmpty().WithErrorCode("VAL.ShortlistId.Required");
    }
}
