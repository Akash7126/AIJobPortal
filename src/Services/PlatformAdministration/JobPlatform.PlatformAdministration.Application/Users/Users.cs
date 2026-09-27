using FluentValidation;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.PlatformAdministration.Application.Users;

/// <summary>US-3.1.4-01: the user-management list (job seekers, employers and administrators), composed live from BC-03 (Q-06).</summary>
public sealed record ListPlatformUsersQuery(string? Search, string? Type, string? Status, int Page = 1, int PageSize = 20)
    : AdminQuery<PagedResult<PlatformUserListItem>>;

public static class UserFilters
{
    public static readonly IReadOnlyList<string> Types = new[] { "JobSeeker", "Employer", "Administrator" };
    public const int MaxSearchLength = 100;
}

public sealed class ListPlatformUsersValidator : AbstractValidator<ListPlatformUsersQuery>
{
    public ListPlatformUsersValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
        RuleFor(q => q.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");
        RuleFor(q => q.Search).MaximumLength(UserFilters.MaxSearchLength).WithErrorCode("VAL.Search.TooLong");
        RuleFor(q => q.Type).Must(t => UserFilters.Types.Contains(t!, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.Type.Invalid")
            .When(q => q.Type is not null);
        RuleFor(q => q.Status).MaximumLength(50).WithErrorCode("VAL.Status.Invalid").When(q => q.Status is not null);
    }
}

internal sealed class ListPlatformUsersHandler : IQueryHandler<ListPlatformUsersQuery, PagedResult<PlatformUserListItem>>
{
    private readonly IUserDirectory _directory;

    public ListPlatformUsersHandler(IUserDirectory directory) => _directory = directory;

    public Task<Result<PagedResult<PlatformUserListItem>>> Handle(ListPlatformUsersQuery request, CancellationToken ct) =>
        _directory.ListAsync(request.Type, request.Status, request.Search, new PageRequest(request.Page, request.PageSize), ct);
}
