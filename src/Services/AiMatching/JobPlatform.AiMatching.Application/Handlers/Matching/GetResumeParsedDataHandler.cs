using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

internal sealed class GetResumeParsedDataHandler(IMatchReadStore store) : IQueryHandler<GetResumeParsedDataQuery, ResumeParsedDataDto>
{
    public async Task<Result<ResumeParsedDataDto>> Handle(GetResumeParsedDataQuery request, CancellationToken ct) =>
        await store.GetResumeParsedDataAsync(request.ResumeParsedDataId, ct) is { } dto ? dto : Error.NotFound(AiErrorCodes.NotFound, "The parsed data was not found.");
}
