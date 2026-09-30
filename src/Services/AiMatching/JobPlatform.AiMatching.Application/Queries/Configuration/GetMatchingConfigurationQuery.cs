using JobPlatform.AiMatching.Application.DTOs.Configuration;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Queries.Configuration;

public sealed record GetMatchingConfigurationQuery : AdminRequest, IQuery<MatchingConfigurationDto>;
