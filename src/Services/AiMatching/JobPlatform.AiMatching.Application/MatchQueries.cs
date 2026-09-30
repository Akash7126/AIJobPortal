using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application;

/// <summary>Owner-only view of an owned posting; unknown postings are 404, foreign postings refused with the story's own code.</summary>
internal static class PostingOwnership
{
    public static async Task<Result<KnownPosting>> RequireOwnedAsync(IKnownPostingRepository postings, ICurrentUser user, Guid postingId, string forbiddenCode,
        CancellationToken ct)
    {
        var posting = await postings.GetAsync(postingId, ct);
        if (posting is null)
        {
            return Error.NotFound(AiErrorCodes.PostingNotFound, "The posting was not found.");
        }

        return posting.EmployerAccountId == user.UserId ? posting : Error.Forbidden(forbiddenCode, "Only the owner of the posting may see its candidates.");
    }
}
