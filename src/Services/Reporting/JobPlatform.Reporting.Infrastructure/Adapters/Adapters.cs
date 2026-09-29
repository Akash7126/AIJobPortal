using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JobPlatform.Reporting.Application;
using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Application.DTOs.Common;
using JobPlatform.Reporting.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobPlatform.Reporting.Infrastructure.Adapters;

// Anti-corruption adapters to external systems (foundation 9.5). Each port has a deterministic simulated adapter and a real HTTP adapter, chosen by configuration:
//   Metrics:Provider  = Simulated | Prometheus | None
//   Sessions:Provider = Simulated | Http | None
//   PowerBi:Provider  = Simulated | Http

/// <summary>Deterministic telemetry stand-in: stable values inside normal ranges (used in Development and tests).</summary>
public sealed class SimulatedMetricsSource : IMetricsSource
{
    public Task<IReadOnlyDictionary<string, decimal>> SampleAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>
        {
            [PerformanceMetrics.ResponseTimeP95Ms] = 420m,
            [PerformanceMetrics.CpuUtilisationPercent] = 35m,
            [PerformanceMetrics.MemoryUtilisationPercent] = 52m,
            [PerformanceMetrics.ErrorRatePercent] = 0.4m,
            [PerformanceMetrics.RequestsPerSecond] = 85m
        });
}

/// <summary>Prometheus instant-query adapter: one PromQL expression per metric (Metrics:Prometheus:Queries:&lt;metric&gt;). A metric whose query fails or returns nothing is omitted.</summary>
public sealed class PrometheusMetricsSource : IMetricsSource
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PrometheusMetricsSource> _logger;

    public PrometheusMetricsSource(IHttpClientFactory http, IConfiguration configuration, ILogger<PrometheusMetricsSource> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> SampleAsync(CancellationToken ct = default)
    {
        var baseUrl = _configuration["Metrics:Prometheus:BaseUrl"];
        var result = new Dictionary<string, decimal>();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return result;
        }

        var client = _http.CreateClient("prometheus");
        client.Timeout = TimeSpan.FromSeconds(5);
        foreach (var metric in PerformanceMetrics.Known)
        {
            var query = _configuration[$"Metrics:Prometheus:Queries:{metric}"];
            if (string.IsNullOrWhiteSpace(query))
            {
                continue;
            }

            try
            {
                using var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/api/v1/query?query={Uri.EscapeDataString(query)}", ct);
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var series = doc.RootElement.GetProperty("data").GetProperty("result");
                if (series.GetArrayLength() > 0 && decimal.TryParse(series[0].GetProperty("value")[1].GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    result[metric] = value;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Prometheus query for {Metric} failed; the metric degrades to no data", metric);
            }
        }

        return result;
    }
}

/// <summary>Source-not-configured stand-in: the metrics degrade to "no data".</summary>
public sealed class NoMetricsSource : IMetricsSource
{
    public Task<IReadOnlyDictionary<string, decimal>> SampleAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>());
}

/// <summary>Deterministic session list (no BC-03 endpoint exists yet: handover Q-03). Two anonymous sessions with profile links.</summary>
public sealed class SimulatedSessionSource : ISessionSource
{
    private readonly TimeProvider _clock;

    public SimulatedSessionSource(TimeProvider clock) => _clock = clock;

    public Task<IReadOnlyList<ActiveSession>?> GetActiveAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        IReadOnlyList<ActiveSession> sessions = new[]
        {
            new ActiveSession("sim-1", "JobSeeker", now.AddMinutes(-20), now.AddMinutes(-1), "/api/v1/profiles/00000000-0000-0000-0000-000000000001"),
            new ActiveSession("sim-2", "Employer", now.AddMinutes(-45), now.AddMinutes(-5), "/api/v1/employers/00000000-0000-0000-0000-000000000002")
        };
        return Task.FromResult<IReadOnlyList<ActiveSession>?>(sessions);
    }
}

/// <summary>Calls BC-03's proposed <c>GET /internal/v1/sessions/active</c> with a client-credentials token. Any failure returns null so the dashboard degrades.</summary>
public sealed class HttpSessionSource : ISessionSource
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpSessionSource> _logger;

    public HttpSessionSource(IHttpClientFactory http, IConfiguration configuration, ILogger<HttpSessionSource> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    private sealed record SessionItem(string SessionKey, string ActorType, DateTime StartedAtUtc, DateTime LastSeenAtUtc, string? ProfileLink);

    public async Task<IReadOnlyList<ActiveSession>?> GetActiveAsync(CancellationToken ct = default)
    {
        var baseUrl = _configuration["Identity:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        try
        {
            var client = _http.CreateClient("identity-internal");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(2);
            var tokenResponse = await client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _configuration["Identity:ClientId"] ?? string.Empty,
                ["client_secret"] = _configuration["Identity:ClientSecret"] ?? string.Empty
            }), ct);
            tokenResponse.EnsureSuccessStatusCode();
            using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));

            using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/v1/sessions/active");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("access_token").GetString());
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<SessionItem>>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? new List<SessionItem>();
            return items.Select(i => new ActiveSession(i.SessionKey, i.ActorType, i.StartedAtUtc, i.LastSeenAtUtc, i.ProfileLink ?? string.Empty)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Active sessions could not be read from Account Identity; the login dashboard degrades");
            return null;
        }
    }
}

/// <summary>Power BI stand-in: returns a deterministic report URL without any network call.</summary>
public sealed class SimulatedPowerBiExporter : IPowerBiExporter
{
    public Task<PowerBiPublication> PublishAsync(string reportName, ReportTable table, CancellationToken ct = default)
    {
        var id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reportName)))[..12].ToLowerInvariant();
        return Task.FromResult(new PowerBiPublication(id, $"https://app.powerbi.example/reports/{id}"));
    }
}

/// <summary>Real Power BI REST adapter (push dataset): POST {PowerBi:BaseUrl}/datasets with the table and a bearer token from PowerBi:AccessToken. The caller applies timeout and retries.</summary>
public sealed class HttpPowerBiExporter : IPowerBiExporter
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;

    public HttpPowerBiExporter(IHttpClientFactory http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    public async Task<PowerBiPublication> PublishAsync(string reportName, ReportTable table, CancellationToken ct = default)
    {
        var baseUrl = _configuration["PowerBi:BaseUrl"] ?? throw new InvalidOperationException("PowerBi:BaseUrl is not configured.");
        var client = _http.CreateClient("powerbi");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/datasets?defaultRetentionPolicy=None")
        {
            Content = JsonContent.Create(new
            {
                name = reportName,
                tables = new[]
                {
                    new
                    {
                        name = "Report",
                        columns = table.Columns.Select(c => new { name = c.Name, dataType = c.Kind == "measure" ? "Double" : "String" }).ToArray()
                    }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["PowerBi:AccessToken"]);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        return new PowerBiPublication(id, $"{baseUrl.TrimEnd('/')}/datasets/{id}");
    }
}
