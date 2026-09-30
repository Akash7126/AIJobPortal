using JobPlatform.Reporting.Application.DTOs.Activity;

namespace JobPlatform.Reporting.Application.Interfaces;

/// <summary>Anti-corruption port to BC-03's proposed <c>GET /internal/v1/sessions/active</c> (Q-03). Null = source unavailable (the dashboard degrades).</summary>
public interface ISessionSource
{
    Task<IReadOnlyList<ActiveSession>?> GetActiveAsync(CancellationToken ct = default);
}
