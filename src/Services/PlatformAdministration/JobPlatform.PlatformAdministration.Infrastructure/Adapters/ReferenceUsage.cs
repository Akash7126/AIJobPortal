using System.Net.Http.Json;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.PlatformAdministration.Application;
using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobPlatform.PlatformAdministration.Infrastructure.Adapters;

/// <summary>
/// Adapter (ReferenceUsage:Provider = Http) asking every configured owner (ReferenceUsage:BaseUrls, e.g. BC-09 and BC-10) which entries are still
/// referenced (handover 6.2, Q-07). Any failure means "unavailable": the application then requires explicit confirmation (fail-safe).
/// </summary>
internal sealed class HttpReferenceUsageChecker : IReferenceUsageChecker
{
    public const string ClientName = "reference-usage";

    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpReferenceUsageChecker> _logger;

    public HttpReferenceUsageChecker(IHttpClientFactory factory, IConfiguration configuration, ILogger<HttpReferenceUsageChecker> logger)
    {
        _factory = factory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ReferenceUsageResult> CheckAsync(string referenceType, IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        var owners = _configuration.GetSection("ReferenceUsage:BaseUrls").GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (owners.Count == 0)
        {
            return new ReferenceUsageResult(false, Array.Empty<string>());
        }

        var inUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var owner in owners)
        {
            try
            {
                var http = _factory.CreateClient(ClientName);
                http.BaseAddress = new Uri(owner!.TrimEnd('/') + "/");
                using var response = await http.PostAsJsonAsync("internal/v1/reference-usage/check", new ReferenceUsageCheckRequest(referenceType, codes.ToArray()), ct);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<ReferenceUsageCheckResponse>(InternalApiClientExtensions.Json, ct);
                foreach (var code in body?.InUse ?? Array.Empty<string>())
                {
                    inUse.Add(code);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Reference usage check against {Owner} failed; treating usage as unknown", owner);
                return new ReferenceUsageResult(false, Array.Empty<string>());
            }
        }

        return new ReferenceUsageResult(true, inUse.ToList());
    }
}

/// <summary>Local double (ReferenceUsage:Provider = Fake, the development default): reports the codes listed in ReferenceUsage:FakeInUse as referenced.</summary>
internal sealed class FakeReferenceUsageChecker : IReferenceUsageChecker
{
    private readonly IConfiguration _configuration;

    public FakeReferenceUsageChecker(IConfiguration configuration) => _configuration = configuration;

    public Task<ReferenceUsageResult> CheckAsync(string referenceType, IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        var configured = _configuration.GetSection("ReferenceUsage:FakeInUse").GetChildren().Select(c => c.Value).Where(v => v is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(new ReferenceUsageResult(true, codes.Where(configured.Contains).ToList()));
    }
}
