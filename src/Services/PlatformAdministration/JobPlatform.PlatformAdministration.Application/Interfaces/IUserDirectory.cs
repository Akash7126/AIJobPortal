using JobPlatform.PlatformAdministration.Application.DTOs.Users;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.PlatformAdministration.Application.Interfaces;

/// <summary>Anti-corruption port to the user directory of BC-03 (US-3.1.4-01, Q-06: live composition). Adapter chosen by UserDirectory:Provider.</summary>
public interface IUserDirectory
{
    Task<JobPlatform.SharedKernel.Application.Results.Result<PagedResult<PlatformUserListItem>>> ListAsync(
        string? type, string? status, string? search, PageRequest page, CancellationToken ct = default);
}
