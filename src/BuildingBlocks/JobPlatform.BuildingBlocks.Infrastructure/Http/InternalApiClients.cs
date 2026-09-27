using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.BuildingBlocks.Infrastructure.Http;

/// <summary>
/// Adds a service-to-service bearer token (OAuth 2.0 client-credentials from BC-03, foundation section 9.5) to outgoing internal API calls.
/// Configuration: Identity:BaseUrl, Identity:ClientId, Identity:ClientSecret. Without a BaseUrl no token is added (local development against doubles).
/// </summary>
public sealed class InternalServiceTokenHandler : DelegatingHandler
{
    public const string TokenClientName = "identity-internal-token";

    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTime _expires;

    public InternalServiceTokenHandler(IHttpClientFactory factory, IConfiguration configuration, TimeProvider clock)
    {
        _factory = factory;
        _configuration = configuration;
        _clock = clock;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, ct);
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        var baseUrl = _configuration["Identity:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (_token is not null && _expires > now)
        {
            return _token;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && _expires > now)
            {
                return _token;
            }

            var http = _factory.CreateClient(TokenClientName);
            http.BaseAddress = new Uri(baseUrl);
            var response = await http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _configuration["Identity:ClientId"] ?? string.Empty,
                ["client_secret"] = _configuration["Identity:ClientSecret"] ?? string.Empty
            }), ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            _token = doc.RootElement.GetProperty("access_token").GetString();
            _expires = now.AddSeconds(Math.Max(30, doc.RootElement.GetProperty("expires_in").GetInt32() - 30));
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>Retries idempotent GETs (network failure, 5xx, 429) up to twice with jitter (foundation section 9.5). Everything else passes through.</summary>
public sealed class RetryIdempotentGetHandler : DelegatingHandler
{
    private const int MaxRetries = 2;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, ct);
                if (request.Method != HttpMethod.Get || attempt >= MaxRetries || !IsTransient(response.StatusCode))
                {
                    return response;
                }

                response.Dispose();
            }
            catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !ct.IsCancellationRequested)
            {
                if (request.Method != HttpMethod.Get || attempt >= MaxRetries)
                {
                    throw;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1) + Random.Shared.Next(0, 100)), ct);
        }
    }

    private static bool IsTransient(HttpStatusCode status) => (int)status >= 500 || status == HttpStatusCode.TooManyRequests;
}

public static class InternalApiClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Registers a typed HttpClient for another BC's internal API: base address from <paramref name="baseUrlKey"/> (e.g. Downstream:JobPosting:BaseUrl),
    /// timeout Downstream:TimeoutSeconds (default 2 s), service token, retry of idempotent GETs.
    /// </summary>
    public static IHttpClientBuilder AddInternalApiClient<TClient, TImplementation>(this IServiceCollection services, string baseUrlKey)
        where TClient : class where TImplementation : class, TClient
    {
        services.AddHttpClient(InternalServiceTokenHandler.TokenClientName);
        services.AddTransient<InternalServiceTokenHandler>();
        services.AddTransient<RetryIdempotentGetHandler>();
        return services.AddHttpClient<TClient, TImplementation>((sp, client) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var baseUrl = configuration[baseUrlKey];
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                }

                client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Downstream:TimeoutSeconds", 2));
            })
            .AddHttpMessageHandler<RetryIdempotentGetHandler>()
            .AddHttpMessageHandler<InternalServiceTokenHandler>();
    }

    /// <summary>GET returning the body, or null on 404. Other failures throw <see cref="InternalApiException"/> (the caller degrades or lets the inbox retry).</summary>
    public static async Task<T?> GetOrNullAsync<T>(this HttpClient client, string relativeUrl, CancellationToken ct) where T : class
    {
        if (client.BaseAddress is null)
        {
            throw new InternalApiException($"No base address is configured for the downstream API ({relativeUrl}).", null);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(relativeUrl, ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !ct.IsCancellationRequested)
        {
            throw new InternalApiException($"The downstream API is unavailable ({relativeUrl}).", null, ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InternalApiException($"The downstream API answered {(int)response.StatusCode} ({relativeUrl}).", response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
    }
}

/// <summary>The other BC could not be reached or refused the call (maps to ErrorType.External / a retryable inbox failure).</summary>
public sealed class InternalApiException : Exception
{
    public InternalApiException(string message, HttpStatusCode? status, Exception? inner = null) : base(message, inner) => Status = status;

    public HttpStatusCode? Status { get; }
}
