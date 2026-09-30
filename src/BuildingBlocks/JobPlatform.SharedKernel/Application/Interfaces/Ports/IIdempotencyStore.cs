using JobPlatform.SharedKernel.Application.Ports;

namespace JobPlatform.SharedKernel.Application.Interfaces.Ports;

public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> GetAsync(string scope, string key, CancellationToken ct = default);

    /// <summary>Reserves the key. False when it already exists (completed or in flight).</summary>
    Task<bool> TryBeginAsync(string scope, string key, string fingerprint, TimeSpan ttl, CancellationToken ct = default);

    Task CompleteAsync(string scope, string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default);

    Task ReleaseAsync(string scope, string key, CancellationToken ct = default);
}
