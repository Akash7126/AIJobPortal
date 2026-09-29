using JobPlatform.AiMatching.Application.Commands.Shortlists;
using JobPlatform.AiMatching.Application.DTOs.Shortlists;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Shortlists;

internal sealed class ComputeCandidateShortlistHandler(IKnownPostingRepository postings, ICandidateShortlistRepository shortlists, IWorkItemRepository work,
    IMatchingConfigurationProvider configuration, ICurrentUser user, TimeProvider clock) : ICommandHandler<ComputeCandidateShortlistCommand, ShortlistDto>
{
    public async Task<Result<ShortlistDto>> Handle(ComputeCandidateShortlistCommand request, CancellationToken ct)
    {
        var owned = await PostingOwnership.RequireOwnedAsync(postings, user, request.JobPostingId, AiErrorCodes.Forbidden, ct);
        if (owned.IsFailure)
        {
            return owned.Error!;
        }

        var config = await configuration.GetAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var shortlist = CandidateShortlist.Request(request.JobPostingId, owned.Value.EmployerAccountId, user.ToActor(), request.Size ?? config.ShortlistSize, config, now);
        shortlists.Add(shortlist);
        await work.EnqueueAsync(WorkItemKind.ComputeShortlist, shortlist.Id, now, ct);
        return ToDto(shortlist);
    }

    internal static ShortlistDto ToDto(CandidateShortlist s) => new(s.Id, s.JobPostingId, s.EmployerAccountId, s.Status.ToString(), s.RequestedSize, s.ConfigVersion,
        s.Items.Select(i => new ShortlistEntryDto(i.Rank, i.ProfileId, i.Score)).ToArray(), s.FailureReason, s.RequestedAtUtc, s.ComputedAtUtc);
}
