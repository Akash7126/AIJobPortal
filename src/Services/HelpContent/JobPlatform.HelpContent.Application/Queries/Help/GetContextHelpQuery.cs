using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.HelpContent.Application.Queries.Help;

/// <summary>US-3.7.2-04: an unmapped page key falls back to the general help center rather than an error (AC-02).</summary>
public sealed record GetContextHelpQuery(string PageKey) : IQuery<HelpContentView?>;
