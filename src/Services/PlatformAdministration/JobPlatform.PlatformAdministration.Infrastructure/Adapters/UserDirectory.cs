using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.PlatformAdministration.Application.DTOs.Users;
using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JobPlatform.PlatformAdministration.Infrastructure.Adapters;

/// <summary>
/// Anti-corruption adapter (UserDirectory:Provider = Http) over BC-03's administrator list (GET /api/v1/admin/accounts). The caller's own
/// bearer token is forwarded (end-user identity propagated, foundation 9.5), so BC-03 stays the authority on who may list accounts.
/// </summary>
internal sealed class HttpUserDirectory : IUserDirectory
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<HttpUserDirectory> _logger;

    public HttpUserDirectory(HttpClient http, IHttpContextAccessor accessor, ILogger<HttpUserDirectory> logger)
    {
        _http = http;
        _accessor = accessor;
        _logger = logger;
    }

    public async Task<Result<PagedResult<PlatformUserListItem>>> ListAsync(string? type, string? status, string? search, PageRequest page, CancellationToken ct = default)
    {
        var unavailable = Error.External(ErrorCodes.UsersUnavailable, "The user directory is unavailable. Try again later.") with { RetryAfter = TimeSpan.FromSeconds(30) };
        if (_http.BaseAddress is null)
        {
            return unavailable;
        }

        var query = new List<string> { $"page={page.Page}", $"pageSize={page.PageSize}" };
        if (type is not null)
        {
            query.Add($"actorType={Uri.EscapeDataString(type)}");
        }

        if (status is not null)
        {
            query.Add($"standing={Uri.EscapeDataString(status)}");
        }

        if (search is not null)
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/admin/accounts?" + string.Join('&', query));
        var authorization = _accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var header))
        {
            request.Headers.Authorization = header;
        }

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BC-03 account list answered {Status}", (int)response.StatusCode);
                return unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<AccountPage>(InternalApiClientExtensions.Json, ct);
            if (body is null)
            {
                return unavailable;
            }

            var items = body.Items.Select(a => new PlatformUserListItem(a.AccountId, a.ActorType, a.DisplayName, a.Email, a.Mobile, a.Standing, a.CreatedAtUtc)).ToList();
            return new PagedResult<PlatformUserListItem>(items, body.Page, body.PageSize, body.TotalCount);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "BC-03 account list failed");
            return unavailable;
        }
    }

    // Shape of BC-03's paged account list (only what the composition needs).
    private sealed record AccountPage(IReadOnlyList<AccountItem> Items, int Page, int PageSize, int TotalCount);

    private sealed record AccountItem(Guid AccountId, string ActorType, string DisplayName, string? Email, string? Mobile, string Standing, DateTime CreatedAtUtc);
}

/// <summary>Local double (UserDirectory:Provider = Fake, the development default): a small fixed directory that honours the filters.</summary>
internal sealed class FakeUserDirectory : IUserDirectory
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<PlatformUserListItem> Users = new[]
    {
        new PlatformUserListItem(Guid.Parse("11111111-1111-1111-1111-111111111111"), "JobSeeker", "Layla Haddad", "l***@example.org", "+970****0001", "Active", Epoch),
        new PlatformUserListItem(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Employer", "Al-Quds Software Ltd", "h***@example.org", "+970****0002", "Active", Epoch.AddDays(1)),
        new PlatformUserListItem(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Administrator", "Platform Admin", "a***@example.org", "+970****0003", "Active", Epoch.AddDays(2)),
        new PlatformUserListItem(Guid.Parse("44444444-4444-4444-4444-444444444444"), "JobSeeker", "Omar Nasser", "o***@example.org", "+970****0004", "Pending", Epoch.AddDays(3))
    };

    public Task<Result<PagedResult<PlatformUserListItem>>> ListAsync(string? type, string? status, string? search, PageRequest page, CancellationToken ct = default)
    {
        var query = Users.AsEnumerable();
        if (type is not null)
        {
            query = query.Where(u => string.Equals(u.ActorType, type, StringComparison.OrdinalIgnoreCase));
        }

        if (status is not null)
        {
            query = query.Where(u => string.Equals(u.Standing, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var all = query.ToList();
        var items = all.Skip(page.Skip).Take(page.PageSize).ToList();
        return Task.FromResult<Result<PagedResult<PlatformUserListItem>>>(new PagedResult<PlatformUserListItem>(items, page.Page, page.PageSize, all.Count));
    }
}
