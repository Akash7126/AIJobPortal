using JobPlatform.JobSeekerProfile.Application.DTOs.Documents;
using JobPlatform.JobSeekerProfile.Application.Queries.Documents;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Documents;

internal sealed class ListSupplementaryDocumentsHandler(IProfileRepository profiles, IProfileReadStore reads, ICurrentUser user)
    : IQueryHandler<ListSupplementaryDocumentsQuery, IReadOnlyList<DocumentView>>
{
    public async Task<Result<IReadOnlyList<DocumentView>>> Handle(ListSupplementaryDocumentsQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        return Result.Success(await reads.ListDocumentsAsync(DocumentOwnerType.JobSeekerProfile, profile.OwnerAccountId, ct));
    }
}
