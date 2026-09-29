using FluentValidation;
using JobPlatform.AiMatching.Application.Queries.Matching;

namespace JobPlatform.AiMatching.Application.Validators.Matching;

public sealed class GetMatchScoreValidator : AbstractValidator<GetMatchScoreQuery>
{
    public GetMatchScoreValidator() => RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
}
