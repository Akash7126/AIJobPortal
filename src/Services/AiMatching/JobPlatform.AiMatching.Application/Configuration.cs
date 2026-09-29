using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application;

/// <summary>A configuration change applies to the next computation: evict the cached snapshot right after the commit (cache key config:current).</summary>
internal sealed class ConfigurationChangedHandler(IMatchingConfigurationProvider provider) : IDomainEventHandler<MatchingConfigurationChangedDomainEvent>
{
    public Task Handle(MatchingConfigurationChangedDomainEvent domainEvent, CancellationToken ct) => provider.InvalidateAsync(ct);
}
