using JobPlatform.SharedKernel.Application.Ports;

namespace JobPlatform.SharedKernel.Application.Interfaces.Ports;

public interface IRateLimiter
{
    /// <summary>Counts one attempt for the key and reports whether it is still within the permits for the window.</summary>
    Task<RateLimitDecision> HitAsync(string key, int permits, TimeSpan window, CancellationToken ct = default);

    Task<long> CountAsync(string key, CancellationToken ct = default);

    Task ResetAsync(string key, CancellationToken ct = default);
}
