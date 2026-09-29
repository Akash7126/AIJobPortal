using FluentValidation;
using JobPlatform.JobPosting.Application.Commands.SavedSearches;
using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.Validators.SavedSearches;

public sealed class SaveSearchValidator : AbstractValidator<SaveSearchCommand>
{
    public SaveSearchValidator()
    {
        RuleFor(c => c).Must(c => c.Keyword is not null || c.Governorate is not null || c.City is not null || c.SalaryMin is not null
                || c.SalaryMax is not null || c.ContractType is not null || c.CategoryCode is not null || c.PostedAfterUtc is not null
                || c.DeadlineBeforeUtc is not null)
            .WithErrorCode("VAL.Criteria.AtLeastOneRequired");
        RuleFor(c => c).Must(c => c.SalaryMin is null || c.SalaryMax is null || c.SalaryMin <= c.SalaryMax)
            .WithErrorCode(ErrorCodes.SearchInvalidField).WithName("salary");
        RuleFor(c => c.ContractType).Must(v => v is null || Enum.TryParse<ContractType>(v, true, out _)).WithErrorCode("VAL.ContractType.Invalid");
    }
}
