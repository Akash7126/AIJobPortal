using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.AiMatching.Application;

// Inbox handlers of BC-10 (handover 5.2). Each is an application use case run inside the inbox transaction: safe to run twice, tolerant of out-of-order delivery
// (a missing prerequisite throws so the inbox retries), and database-only except where the handover requires a fetch. Long computations are queued for the worker.

/// <summary>ResumeCreated (BC-04) -> parse, standardise, publish ResumeParsedDataComputed and SkillStandardizationUpdated.</summary>
public sealed class ResumeCreatedHandler(IRequestHandler<ParseResumeCommand, Unit> parse) : IIntegrationEventHandler<ResumeCreatedIntegrationEvent>
{
    public async Task Handle(ResumeCreatedIntegrationEvent e, CancellationToken ct) =>
        InboxOutcome.ThrowIfFailed(await parse.Handle(new ParseResumeCommand(e.ResumeId, e.ProfileId, e.Format, e.SizeBytes, e.Sha256), ct));
}

/// <summary>JobPostingCreated (BC-09) -> replica, semantics, embedding; matching for the posting is queued.</summary>
public sealed class JobPostingCreatedHandler(IKnownPostingRepository postings, IRequestHandler<AnalyzeJobDescriptionCommand, Unit> analyze, IWorkItemRepository work,
    TimeProvider clock) : IIntegrationEventHandler<JobPostingCreatedIntegrationEvent>
{
    public async Task Handle(JobPostingCreatedIntegrationEvent e, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var known = await postings.GetAsync(e.JobPostingId, ct);
        if (known is null)
        {
            postings.Add(KnownPosting.Create(e.JobPostingId, e.EmployerAccountId, e.Status, e.Title, e.AggregateVersion, now));
        }
        else
        {
            known.ApplyStatus(e.Status, e.AggregateVersion, now);
        }

        var seed = new PostingSource(e.JobPostingId, e.EmployerAccountId, e.Status, false, e.AggregateVersion, e.Title, e.Category, null, e.Skills, null,
            Array.Empty<string>(), e.Location, null, WorkArrangement.OnSite, null, null, e.SalaryMin, e.SalaryMax);
        InboxOutcome.ThrowIfFailed(await analyze.Handle(new AnalyzeJobDescriptionCommand(seed), ct));
        if (seed.IsActive)
        {
            await work.EnqueueAsync(WorkItemKind.MatchesForPosting, e.JobPostingId, now, ct);
        }
    }
}

/// <summary>Shared reaction to a posting status change: a posting that is no longer active is withdrawn, an active one is re-matched.</summary>
public sealed class PostingStatusReaction(IKnownPostingRepository postings, IRequestHandler<WithdrawPostingCommand, Unit> withdraw, IWorkItemRepository work, TimeProvider clock)
{
    public async Task ApplyAsync(Guid postingId, string toStatus, long aggregateVersion, CancellationToken ct)
    {
        var known = await postings.GetAsync(postingId, ct)
                    ?? throw new InvalidOperationException($"Posting {postingId} is not known yet; retrying after JobPostingCreated arrives.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (!known.ApplyStatus(toStatus, aggregateVersion, now))
        {
            return; // stale or duplicate
        }

        if (known.IsActive)
        {
            await work.EnqueueAsync(WorkItemKind.MatchesForPosting, postingId, now, ct);
        }
        else
        {
            InboxOutcome.ThrowIfFailed(await withdraw.Handle(new WithdrawPostingCommand(postingId), ct));
        }
    }
}

public sealed class JobPostingUpdatedHandler(PostingStatusReaction reaction) : IIntegrationEventHandler<JobPostingUpdatedIntegrationEvent>
{
    public Task Handle(JobPostingUpdatedIntegrationEvent e, CancellationToken ct) => reaction.ApplyAsync(e.JobPostingId, e.ToStatus, e.AggregateVersion, ct);
}

public sealed class JobPostingStatusUpdatedHandler(PostingStatusReaction reaction) : IIntegrationEventHandler<JobPostingStatusUpdatedIntegrationEvent>
{
    public Task Handle(JobPostingStatusUpdatedIntegrationEvent e, CancellationToken ct) => reaction.ApplyAsync(e.JobPostingId, e.ToStatus, e.AggregateVersion, ct);
}

/// <summary>JobOfferingSuspended (BC-08) -> the posting is withdrawn from matching.</summary>
public sealed class JobOfferingSuspendedHandler(IKnownPostingRepository postings, IRequestHandler<WithdrawPostingCommand, Unit> withdraw, TimeProvider clock)
    : IIntegrationEventHandler<JobOfferingSuspendedIntegrationEvent>
{
    public async Task Handle(JobOfferingSuspendedIntegrationEvent e, CancellationToken ct)
    {
        if (await postings.GetAsync(e.JobPostingId, ct) is { } known)
        {
            known.Suspend(clock.GetUtcNow().UtcDateTime);
        }

        InboxOutcome.ThrowIfFailed(await withdraw.Handle(new WithdrawPostingCommand(e.JobPostingId), ct));
    }
}

/// <summary>PlatformTaxonomyUpdated (BC-08) -> evict the cached taxonomy and queue re-standardisation; runs already in progress keep their version.</summary>
public sealed class PlatformTaxonomyUpdatedHandler(ISkillTaxonomyProvider taxonomies, IWorkItemRepository work, TimeProvider clock)
    : IIntegrationEventHandler<PlatformTaxonomyUpdatedIntegrationEvent>
{
    public async Task Handle(PlatformTaxonomyUpdatedIntegrationEvent e, CancellationToken ct)
    {
        if (!string.Equals(e.TaxonomyType, "skills", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await taxonomies.InvalidateAsync(ct);
        await work.EnqueueAsync(WorkItemKind.Restandardize, Guid.Empty, clock.GetUtcNow().UtcDateTime, ct);
    }
}

/// <summary>ProfileCreated (BC-04) -> identity replica (account to profile); embedding and matching are queued.</summary>
public sealed class ProfileCreatedHandler(IKnownProfileRepository profiles, IWorkItemRepository work, TimeProvider clock) : IIntegrationEventHandler<ProfileCreatedIntegrationEvent>
{
    public async Task Handle(ProfileCreatedIntegrationEvent e, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (await profiles.GetAsync(e.ProfileId, ct) is null)
        {
            profiles.Add(KnownProfile.Create(e.ProfileId, e.OwnerAccountId, e.AggregateVersion, now));
        }

        await work.EnqueueAsync(WorkItemKind.RefreshProfileEmbedding, e.ProfileId, now, ct);
    }
}

/// <summary>ProfileUpdated (BC-04) -> stale scores are recomputed (Q-07); older events are ignored.</summary>
public sealed class ProfileUpdatedHandler(IKnownProfileRepository profiles, IWorkItemRepository work, TimeProvider clock) : IIntegrationEventHandler<ProfileUpdatedIntegrationEvent>
{
    public async Task Handle(ProfileUpdatedIntegrationEvent e, CancellationToken ct)
    {
        var known = await profiles.GetAsync(e.ProfileId, ct)
                    ?? throw new InvalidOperationException($"Profile {e.ProfileId} is not known yet; retrying after ProfileCreated arrives.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (known.Touch(e.AggregateVersion, now))
        {
            await work.EnqueueAsync(WorkItemKind.RefreshProfileEmbedding, e.ProfileId, now, ct);
        }
    }
}

internal static class InboxOutcome
{
    /// <summary>A failed command inside an inbox handler must fail the message so the inbox retries it (external failure) or parks it (poison).</summary>
    public static void ThrowIfFailed(Result<Unit> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"{result.Error!.Code}: {result.Error.Message}");
        }
    }
}
