using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.HelpContent.Application.Queries.News;

/// <summary>US-3.7.1-05: personalised feed when profile interests are available (Q-06), else the general feed - never an error
/// (handover section 6.2: "fallback to general feed").</summary>
public sealed record GetNewsFeedQuery : AuthenticatedQuery<IReadOnlyList<NewsListItemView>>
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.JobSeeker };
}
