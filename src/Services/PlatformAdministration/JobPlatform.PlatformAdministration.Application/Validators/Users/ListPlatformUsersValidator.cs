using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Queries.Users;
using JobPlatform.PlatformAdministration.Application.Users;
using JobPlatform.PlatformAdministration.Application.Validators.Common;

namespace JobPlatform.PlatformAdministration.Application.Validators.Users;

public sealed class ListPlatformUsersValidator : AbstractValidator<ListPlatformUsersQuery>
{
    public ListPlatformUsersValidator()
    {
        RuleFor(q => q.Page).ValidPage();
        RuleFor(q => q.PageSize).ValidPageSize();
        RuleFor(q => q.Search).MaximumLength(UserFilters.MaxSearchLength).WithErrorCode("VAL.Search.TooLong");
        RuleFor(q => q.Type).Must(t => UserFilters.Types.Contains(t!, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.Type.Invalid")
            .When(q => q.Type is not null);
        RuleFor(q => q.Status).MaximumLength(50).WithErrorCode("VAL.Status.Invalid").When(q => q.Status is not null);
    }
}
