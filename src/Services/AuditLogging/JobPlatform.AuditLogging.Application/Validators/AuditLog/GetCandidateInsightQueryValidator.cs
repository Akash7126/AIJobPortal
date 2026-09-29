using FluentValidation;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;

namespace JobPlatform.AuditLogging.Application.Validators.AuditLog;

public sealed class GetCandidateInsightQueryValidator : AbstractValidator<GetCandidateInsightQuery>
{
    public GetCandidateInsightQueryValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty().WithErrorCode("VAL.CandidateId.Required");
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
    }
}
