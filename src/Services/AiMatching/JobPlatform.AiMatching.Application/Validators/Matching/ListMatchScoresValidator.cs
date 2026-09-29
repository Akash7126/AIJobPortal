using FluentValidation;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Application.Validators.Common;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AiMatching.Application.Validators.Matching;

public sealed class ListMatchScoresValidator : AbstractValidator<ListMatchScoresQuery>
{
    public ListMatchScoresValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize(PageRequest.MaxPageSize);
        When(x => x.MinScore.HasValue, () => RuleFor(x => x.MinScore!.Value).InclusiveBetween(0m, 100m).OverridePropertyName("MinScore")
            .WithErrorCode("VAL.MinScore.OutOfRange"));
    }
}
