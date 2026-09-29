using JobPlatform.HelpContent.Application.DTOs.Tutorials;

namespace JobPlatform.HelpContent.Application.Queries.Tutorials;

/// <summary>US-3.7.2-07: first access with no progress row means the tutorial is offered; completed means it is not auto-shown again but
/// remains reachable on request (AC-01/02). The tutorial's own content is a HelpContent(Kind=Guide), read like any other help article.</summary>
public sealed record GetOnboardingTutorialQuery(Guid TutorialId) : AuthenticatedQuery<TutorialView>;
