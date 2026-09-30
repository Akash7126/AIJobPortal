namespace JobPlatform.SharedKernel.Application.Ports;

public readonly record struct RateLimitDecision(bool Allowed, long Count, TimeSpan RetryAfter);

public sealed record IdempotencyEntry(string Fingerprint, bool Completed, bool IsSuccess, string? PayloadJson, string? ErrorJson);
