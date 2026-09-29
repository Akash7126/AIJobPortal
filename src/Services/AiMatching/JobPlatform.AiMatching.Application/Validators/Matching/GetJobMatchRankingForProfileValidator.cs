using FluentValidation;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Application.Validators.Common;

namespace JobPlatform.AiMatching.Application.Validators.Matching;

public sealed class GetJobMatchRankingForProfileValidator : AbstractValidator<GetJobMatchRankingForProfileQuery>
{
    public GetJobMatchRankingForProfileValidator()
    {
        RuleFor(x => x.ProfileId).NotEmpty().WithErrorCode("VAL.ProfileId.Required");
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
    }
}
