using FluentValidation;
using JobPlatform.JobPosting.Application.Commands.Interested;
using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.Validators.Interested;

public sealed class AddInterestedListEntryValidator : AbstractValidator<AddInterestedListEntryCommand>
{
    public AddInterestedListEntryValidator()
    {
        RuleFor(c => c.ReferenceType).Must(v => Enum.TryParse<InterestedReferenceType>(v, true, out _)).WithErrorCode("VAL.ReferenceType.Invalid");
        RuleFor(c => c.PostingId).NotNull().WithErrorCode("VAL.PostingId.Required")
            .When(c => Enum.TryParse<InterestedReferenceType>(c.ReferenceType, true, out var t) && t == InterestedReferenceType.Posting);
        RuleFor(c => c).Must(c => c.Keyword is not null || c.Governorate is not null || c.City is not null || c.SalaryMin is not null
                || c.SalaryMax is not null || c.ContractType is not null || c.CategoryCode is not null)
            .WithErrorCode("VAL.Criteria.AtLeastOneRequired")
            .When(c => Enum.TryParse<InterestedReferenceType>(c.ReferenceType, true, out var t) && t == InterestedReferenceType.Filter);
    }
}
