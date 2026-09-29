using FluentValidation;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Application.Validators.Common;

namespace JobPlatform.AiMatching.Application.Validators.Matching;

public sealed class GetJobMatchRankingValidator : AbstractValidator<GetJobMatchRankingQuery>
{
    public GetJobMatchRankingValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        When(x => x.MinScore.HasValue, () => RuleFor(x => x.MinScore!.Value).InclusiveBetween(0m, 100m).OverridePropertyName("MinScore")
            .WithErrorCode("VAL.MinScore.OutOfRange"));
    }
}
