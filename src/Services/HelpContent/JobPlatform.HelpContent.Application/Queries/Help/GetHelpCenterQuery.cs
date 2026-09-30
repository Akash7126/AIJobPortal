using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.HelpContent.Application.Queries.Help;

/// <summary>US-3.7.2-02: content is shown only to its assigned roles - Guest/null means every visitor. "no role assigned" behaves like Guest.</summary>
public sealed record GetHelpCenterQuery(HelpRole? Role) : IQuery<IReadOnlyList<HelpCenterTopicView>>;
