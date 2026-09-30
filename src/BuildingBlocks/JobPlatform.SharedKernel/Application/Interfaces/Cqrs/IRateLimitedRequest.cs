namespace JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

/// <summary>Request throttled per source (registration, activation, ...). Every call counts as an attempt.</summary>
public interface IRateLimitedRequest
{
    string RateLimitScope { get; }
    string RateLimitedErrorCode { get; }
    int RateLimitPermits => 5;
    TimeSpan RateLimitWindow => TimeSpan.FromMinutes(15);

    /// <summary>Extra discriminator appended to the caller source (e.g. account id). Null = source only.</summary>
    string? RateLimitDiscriminator => null;
}
