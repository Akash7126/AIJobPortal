using JobPlatform.AiMatching.Application.DTOs.Configuration;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.AiMatching.Application.Queries.Configuration;

public sealed record GetMatchingConfigurationQuery : AdminRequest, IQuery<MatchingConfigurationDto>;
