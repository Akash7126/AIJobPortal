using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Queries.Offerings;
using JobPlatform.PlatformAdministration.Application.Validators.Common;
using JobPlatform.PlatformAdministration.Domain.Offerings;

namespace JobPlatform.PlatformAdministration.Application.Validators.Offerings;

public sealed class ListJobOfferingsValidator : AbstractValidator<ListJobOfferingsQuery>
{
    public ListJobOfferingsValidator()
    {
        RuleFor(q => q.Page).ValidPage();
        RuleFor(q => q.PageSize).ValidPageSize();
        RuleFor(q => q.Status).Must(s => Enum.TryParse<JobOfferingStatus>(s, true, out var v) && Enum.IsDefined(v)).WithErrorCode("VAL.Status.Invalid")
            .When(q => q.Status is not null);
    }
}
