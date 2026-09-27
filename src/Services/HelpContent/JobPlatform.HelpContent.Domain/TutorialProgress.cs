using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

/// <summary>
/// Proposed aggregate (handover section 3.7, story US-3.7.2-07): (UserId, TutorialId) -&gt; CompletedAtUtc. No progress row means the
/// tutorial has never been offered/completed; once completed it is not auto-shown again but is always reachable on request (AC-01/02).
/// The tutorial's content itself is a HelpContent(Kind=Guide), editable only by administrators - this aggregate tracks only the reader's
/// progress and needs no admin gate.
/// </summary>
public sealed class TutorialProgress : AggregateRoot<Guid>
{
    private TutorialProgress()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TutorialId { get; private set; }

    public DateTime CompletedAtUtc { get; private set; }

    public static TutorialProgress Complete(Guid id, Guid userId, Guid tutorialId, DateTime nowUtc) =>
        new() { Id = id, UserId = userId, TutorialId = tutorialId, CompletedAtUtc = nowUtc };
}
