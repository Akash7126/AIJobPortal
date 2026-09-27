using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.JobSeekerProfile.Application.Inbox;

/// <summary>Replaces the pipeline's sync-read (US-3.1.1-03): upserts the local KnownAccounts replica; ignores non-JobSeeker accounts.</summary>
public sealed class RecordKnownAccountHandler(IKnownAccountRepository accounts, TimeProvider clock) : IIntegrationEventHandler<AccountApprovedIntegrationEvent>
{
    public async Task Handle(AccountApprovedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.ActorType != JobPlatform.SharedKernel.Common.Enums.ActorType.JobSeeker)
        {
            return;
        }

        var existing = await accounts.GetAsync(integrationEvent.AccountId, ct);
        if (existing is null)
        {
            accounts.Add(KnownAccount.Create(integrationEvent.AccountId, integrationEvent.ActorType, clock.GetUtcNow().UtcDateTime));
        }
    }
}

/// <summary>Q-03 (gap): hides the profile from search/share link on suspension or deletion; idempotent (re-suspension is a no-op).</summary>
public sealed class DeactivateProfileHandler(IProfileRepository profiles, IKnownAccountRepository accounts, IProfileShareLinkRepository links,
    TimeProvider clock) : IIntegrationEventHandler<AccountSuspendedIntegrationEvent>
{
    public async Task Handle(AccountSuspendedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        var known = await accounts.GetAsync(integrationEvent.AccountId, ct);
        known?.MarkSuspended(integrationEvent.Standing);

        var profile = await profiles.GetByOwnerAsync(integrationEvent.AccountId, ct);
        if (profile is null)
        {
            return;
        }

        profile.Deactivate(integrationEvent.ActorId, clock.GetUtcNow().UtcDateTime);
        var link = await links.GetActiveByProfileAsync(profile.Id, ct);
        link?.Deactivate(new Domain.Common.Actor(profile.OwnerAccountId));
    }
}

/// <summary>
/// Merges BC-10's extraction (handover section 5.2). Idempotent per resumeParsedDataId (profile.ProcessedParsedData) and tolerant of arriving
/// before the resume record exists locally (retried later by the inbox processor since the profile/resume repositories are looked up defensively).
/// </summary>
public sealed class ApplyExtractedProfileDataHandler(IProfileRepository profiles, IProcessedParsedDataRepository processed, TimeProvider clock)
    : IIntegrationEventHandler<ResumeParsedDataComputedIntegrationEvent>
{
    public async Task Handle(ResumeParsedDataComputedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (await processed.ExistsAsync(integrationEvent.ResumeParsedDataId, ct))
        {
            return;
        }

        var profile = await profiles.GetByIdAsync(integrationEvent.ProfileId, ct);
        if (profile is null)
        {
            // The profile write has not landed yet (out-of-order delivery); the inbox processor retries later.
            throw new InvalidOperationException($"Profile {integrationEvent.ProfileId} not found yet for resume-parsed-data merge.");
        }

        profile.ApplyExtractedData(integrationEvent.Skills, integrationEvent.JobTitles, integrationEvent.YearsOfExperience, profile.OwnerAccountId,
            clock.GetUtcNow().UtcDateTime);
        processed.Add(ProcessedParsedData.Create(integrationEvent.ResumeParsedDataId, clock.GetUtcNow().UtcDateTime));
    }
}

/// <summary>Q-08: applies the job seeker's reviewed corrections so the profile matches what BC-10 now has on file.</summary>
public sealed class ApplyReviewedDataHandler(IProfileRepository profiles, IProcessedParsedDataRepository processed, TimeProvider clock)
    : IIntegrationEventHandler<ParsedProfileDataUpdatedIntegrationEvent>
{
    public async Task Handle(ParsedProfileDataUpdatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (await processed.ExistsAsync(integrationEvent.ParsedProfileDataId, ct))
        {
            return;
        }

        var profile = await profiles.GetByIdAsync(integrationEvent.ProfileId, ct);
        if (profile is null)
        {
            throw new InvalidOperationException($"Profile {integrationEvent.ProfileId} not found yet for reviewed-data merge.");
        }

        profile.ApplyReviewedSkills(integrationEvent.Skills, integrationEvent.ActorId, clock.GetUtcNow().UtcDateTime);
        processed.Add(ProcessedParsedData.Create(integrationEvent.ParsedProfileDataId, clock.GetUtcNow().UtcDateTime));
    }
}

/// <summary>
/// Q-09: wires the consumption foundation asks for. No local reference-data cache is populated by this build (no BC-08 client was built in this pass -
/// see docs/bc-status/BC-04.md); the handler exists so the queue/binding is real and a cache-eviction body is a small follow-up.
/// </summary>
public sealed class RefreshTaxonomyCacheHandler : IIntegrationEventHandler<PlatformTaxonomyUpdatedIntegrationEvent>
{
    public Task Handle(PlatformTaxonomyUpdatedIntegrationEvent integrationEvent, CancellationToken ct) => Task.CompletedTask;
}
