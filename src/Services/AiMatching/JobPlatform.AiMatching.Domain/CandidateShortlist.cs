using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

public sealed record ShortlistItem(Guid ProfileId, decimal Score, int Rank);

/// <summary>
/// Top-N candidates for one posting (handover 3.8 / US-3.3.1-02): ranked by score, ties by lowest profile id, never padded when fewer than N qualify.
/// Batch work: created Queued (API answers 202), computed by the background worker, then Ready or Failed. Owner-only (E-VBMA-FORBIDDEN).
/// </summary>
public sealed class CandidateShortlist : AggregateRoot<Guid>
{
    private CandidateShortlist()
    {
    }

    public Guid JobPostingId { get; private set; }
    public Guid EmployerAccountId { get; private set; }
    public int RequestedSize { get; private set; }
    public int ConfigVersion { get; private set; }
    public ShortlistStatus Status { get; private set; }
    public IReadOnlyList<ShortlistItem> Items { get; private set; } = Array.Empty<ShortlistItem>();
    public string? FailureReason { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? ComputedAtUtc { get; private set; }

    /// <summary>Owner-only: the caller must be the employer that owns the posting.</summary>
    public static CandidateShortlist Request(Guid jobPostingId, Guid postingOwnerAccountId, Actor actor, int size, MatchingConfigSnapshot config, DateTime nowUtc)
    {
        EnsureOwner(postingOwnerAccountId, actor);
        Guard.Ensure(size is >= 1 and <= 10_000, AiRuleCodes.InvalidInput, "The shortlist size must be between 1 and 10000.", AiErrorCodes.InvalidField, BusinessRuleKind.InvalidInput);
        return new CandidateShortlist
        {
            Id = Guid.NewGuid(), JobPostingId = jobPostingId, EmployerAccountId = postingOwnerAccountId, RequestedSize = size, ConfigVersion = config.ConfigVersion,
            Status = ShortlistStatus.Queued, RequestedAtUtc = nowUtc
        };
    }

    public static void EnsureOwner(Guid postingOwnerAccountId, Actor actor) =>
        Guard.Ensure(actor.Type == ActorType.Employer && actor.Id == postingOwnerAccountId, AiRuleCodes.ShortlistNotOwner,
            "Only the employer that owns the posting may see its candidates.", AiErrorCodes.Forbidden, BusinessRuleKind.Forbidden);

    /// <summary>Keeps the best <see cref="RequestedSize"/> candidates (score desc, profile id asc); returns everything when fewer qualify.</summary>
    public void Complete(IEnumerable<(Guid ProfileId, decimal Score)> candidates, DateTime nowUtc)
    {
        Guard.Ensure(Status == ShortlistStatus.Queued, AiRuleCodes.ShortlistInvalidTransition, "Only a queued shortlist can be completed.", null, BusinessRuleKind.Conflict);
        Items = Rank(candidates, RequestedSize);
        Status = ShortlistStatus.Ready;
        ComputedAtUtc = nowUtc;
    }

    public void Fail(string reason, DateTime nowUtc)
    {
        Guard.Ensure(Status == ShortlistStatus.Queued, AiRuleCodes.ShortlistInvalidTransition, "Only a queued shortlist can fail.", null, BusinessRuleKind.Conflict);
        Status = ShortlistStatus.Failed;
        FailureReason = reason.Length > 500 ? reason[..500] : reason;
        ComputedAtUtc = nowUtc;
    }

    public static IReadOnlyList<ShortlistItem> Rank(IEnumerable<(Guid ProfileId, decimal Score)> candidates, int size) =>
        candidates.OrderByDescending(c => c.Score).ThenBy(c => c.ProfileId).Take(size)
            .Select((c, index) => new ShortlistItem(c.ProfileId, c.Score, index + 1)).ToArray();
}
