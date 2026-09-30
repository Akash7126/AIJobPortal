using JobPlatform.AiMatching.Application.DTOs.Shortlists;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Queries.Shortlists;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Shortlists;

internal sealed class GetCandidateShortlistHandler(IMatchReadStore store, ICurrentUser user) : IQueryHandler<GetCandidateShortlistQuery, ShortlistDto>
{
    public async Task<Result<ShortlistDto>> Handle(GetCandidateShortlistQuery request, CancellationToken ct)
    {
        var dto = await store.GetShortlistAsync(request.ShortlistId, ct);
        if (dto is null || dto.JobPostingId != request.JobPostingId)
        {
            return Error.NotFound(AiErrorCodes.NotFound, "The shortlist was not found.");
        }

        return dto.EmployerAccountId == user.UserId ? dto : Error.Forbidden(AiErrorCodes.Forbidden, "Only the owner of the posting may see its shortlist.");
    }
}
