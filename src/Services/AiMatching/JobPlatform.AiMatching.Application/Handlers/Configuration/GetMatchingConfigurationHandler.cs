using JobPlatform.AiMatching.Application.DTOs.Configuration;
using JobPlatform.AiMatching.Application.Queries.Configuration;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Configuration;

internal sealed class GetMatchingConfigurationHandler(IMatchReadStore store) : IQueryHandler<GetMatchingConfigurationQuery, MatchingConfigurationDto>
{
    public async Task<Result<MatchingConfigurationDto>> Handle(GetMatchingConfigurationQuery request, CancellationToken ct) =>
        await store.GetConfigurationAsync(ct) is { } dto
            ? dto
            : Error.NotFound(AiErrorCodes.NotFound, "The matching configuration has not been initialised.");
}
