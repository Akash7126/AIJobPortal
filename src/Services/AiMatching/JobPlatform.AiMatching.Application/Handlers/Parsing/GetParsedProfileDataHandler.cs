using JobPlatform.AiMatching.Application.DTOs.Parsing;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Queries.Parsing;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Parsing;

internal sealed class GetParsedProfileDataHandler(IMatchReadStore store, IMatchingConfigurationProvider configuration, ICurrentUser user)
    : IQueryHandler<GetParsedProfileDataQuery, ParsedProfileDataDto>
{
    public async Task<Result<ParsedProfileDataDto>> Handle(GetParsedProfileDataQuery request, CancellationToken ct)
    {
        var dto = user.UserId is { } owner ? await store.GetParsedProfileDataAsync(owner, ct) : null;
        if (dto is null)
        {
            return Error.NotFound(AiErrorCodes.NotFound, "There is no parsed data yet.");
        }

        return dto with { LowConfidenceThresholdPercent = (await configuration.GetAsync(ct)).LowConfidenceThresholdPercent };
    }
}
