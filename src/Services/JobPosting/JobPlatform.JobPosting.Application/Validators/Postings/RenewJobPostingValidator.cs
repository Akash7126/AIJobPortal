using FluentValidation;
using JobPlatform.JobPosting.Application.Commands.Postings;

namespace JobPlatform.JobPosting.Application.Validators.Postings;

public sealed class RenewJobPostingValidator : AbstractValidator<RenewJobPostingCommand>
{
    public RenewJobPostingValidator()
    {
        RuleFor(c => c.NewDeadlineUtc).GreaterThan(DateTime.UtcNow).WithErrorCode("VAL.Deadline.MustBeFuture")
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddMonths(12)).WithErrorCode("VAL.Deadline.TooFarAhead");
    }
}
