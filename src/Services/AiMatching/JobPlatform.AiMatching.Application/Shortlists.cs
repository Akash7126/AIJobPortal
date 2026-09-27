using FluentValidation;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Options;

namespace JobPlatform.AiMatching.Application;

/// <summary>US-3.3.1-02: request the top-N shortlist of an owned posting. Batch work: answered 202, computed by the worker, polled with GetCandidateShortlistQuery.</summary>
public sealed record ComputeCandidateShortlistCommand(Guid JobPostingId, int? Size = null) : EmployerRequest, ICommand<ShortlistDto>;

public sealed record GetCandidateShortlistQuery(Guid JobPostingId, Guid ShortlistId) : EmployerRequest, IQuery<ShortlistDto>;

/// <summary>Worker side: computes and completes a queued shortlist.</summary>
public sealed record RunCandidateShortlistCommand(Guid ShortlistId) : ICommand;

public sealed class ComputeCandidateShortlistValidator : AbstractValidator<ComputeCandidateShortlistCommand>
{
    public ComputeCandidateShortlistValidator(IOptions<MatchingOptions> options)
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        When(x => x.Size.HasValue, () => RuleFor(x => x.Size!.Value).InclusiveBetween(1, options.Value.MaxShortlistSize).OverridePropertyName("Size")
            .WithErrorCode("VAL.Size.OutOfRange"));
    }
}

public sealed class GetCandidateShortlistValidator : AbstractValidator<GetCandidateShortlistQuery>
{
    public GetCandidateShortlistValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(x => x.ShortlistId).NotEmpty().WithErrorCode("VAL.ShortlistId.Required");
    }
}

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

internal sealed class RunCandidateShortlistHandler(ICandidateShortlistRepository shortlists, MatchComputationService computation, IMatchScoreRepository scores,
    IKnownProfileRepository profiles, IMatchingConfigurationProvider configuration, TimeProvider clock) : ICommandHandler<RunCandidateShortlistCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RunCandidateShortlistCommand request, CancellationToken ct)
    {
        var shortlist = await shortlists.GetAsync(request.ShortlistId, ct);
        if (shortlist is not { Status: ShortlistStatus.Queued })
        {
            return Result.Success(); // already done (redelivery) or unknown
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var config = await configuration.GetAsync(ct);
        await computation.ComputeForPostingAsync(shortlist.JobPostingId, ct);
        var candidates = new List<(Guid, decimal)>();
        foreach (var score in await scores.ListByPostingAsync(shortlist.JobPostingId, ct))
        {
            if (score.Score >= config.MatchThresholdPercent && await profiles.GetAsync(score.ProfileId, ct) is { Standing: KnownStanding.Active })
            {
                candidates.Add((score.ProfileId, score.Score));
            }
        }

        shortlist.Complete(candidates, now); // fewer than N qualify: all of them, no padding
        return Result.Success();
    }
}
