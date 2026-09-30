using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.HelpContent.Application.Queries.Help;

public sealed record GetHelpContentQuery(Guid HelpContentId) : IQuery<HelpContentView>;
