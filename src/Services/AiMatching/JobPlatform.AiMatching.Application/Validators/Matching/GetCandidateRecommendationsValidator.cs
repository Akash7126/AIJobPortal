using FluentValidation;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Application.Validators.Common;

namespace JobPlatform.AiMatching.Application.Validators.Matching;

public sealed class GetCandidateRecommendationsValidator : AbstractValidator<GetCandidateRecommendationsQuery>
{
    public GetCandidateRecommendationsValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
