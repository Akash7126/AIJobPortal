using System.Text.Json;
using JobPlatform.Notification.Application;
using JobPlatform.Notification.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobPlatform.Notification.Infrastructure.Adapters;

/// <summary>
/// Real adapter for BC-03's contact API (GET /internal/v1/accounts/{id}/contact returning email, mobile, locale; handover 6.2, Q-06). It needs a
/// client-credentials token (Identity:ClientId/ClientSecret) and fails soft: unreachable or 404 means "no contact", so the message stays for retry.
/// BC-03 does not publish this endpoint yet (see docs/bc-status/BC-13.md), which is why Contacts:Provider defaults to Fake in Development.
/// </summary>
public sealed class HttpAccountContactApi : IAccountContactApi
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpAccountContactApi> _logger;
    private string? _token;
    private DateTime _tokenExpires;

    public HttpAccountContactApi(IHttpClientFactory http, IConfiguration configuration, ILogger<HttpAccountContactApi> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RecipientContact?> GetContactAsync(Guid accountId, CancellationToken ct = default)
    {
        var baseUrl = _configuration["Identity:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        try
        {
            var client = _http.CreateClient("identity-contact");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(2);
            await EnsureTokenAsync(client, ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/v1/accounts/{accountId}/contact");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return new RecipientContact(root.TryGetProperty("email", out var e) ? e.GetString() : null, root.TryGetProperty("mobile", out var m) ? m.GetString() : null,
                root.TryGetProperty("locale", out var l) ? l.GetString() ?? "en" : "en");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Contact lookup for {AccountId} failed", accountId);
            return null;
        }
    }

    private async Task EnsureTokenAsync(HttpClient client, CancellationToken ct)
    {
        if (_token is not null && _tokenExpires > DateTime.UtcNow)
        {
            return;
        }

        var response = await client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _configuration["Identity:ClientId"] ?? string.Empty,
            ["client_secret"] = _configuration["Identity:ClientSecret"] ?? string.Empty
        }), ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        _token = doc.RootElement.GetProperty("access_token").GetString();
        _tokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(30, doc.RootElement.GetProperty("expires_in").GetInt32() - 30));
    }
}
