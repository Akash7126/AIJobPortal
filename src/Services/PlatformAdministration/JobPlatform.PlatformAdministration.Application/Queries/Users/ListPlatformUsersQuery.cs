using JobPlatform.PlatformAdministration.Application.DTOs.Users;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.PlatformAdministration.Application.Queries.Users;

/// <summary>US-3.1.4-01: the user-management list (job seekers, employers and administrators), composed live from BC-03 (Q-06).</summary>
public sealed record ListPlatformUsersQuery(string? Search, string? Type, string? Status, int Page = 1, int PageSize = 20)
    : AdminQuery<PagedResult<PlatformUserListItem>>;
