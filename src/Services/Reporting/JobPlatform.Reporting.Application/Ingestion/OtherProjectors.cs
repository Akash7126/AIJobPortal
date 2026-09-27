using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;

namespace JobPlatform.Reporting.Application.Ingestion;

// FactRegistration (milestones), FactNotification, FactMatch and FactOutcome projectors.

public sealed class AccountCreatedProjector(IFactStore facts) : IFactProjector<AccountCreatedIntegrationEvent>
{
    public Task ProjectAsync(AccountCreatedIntegrationEvent e, CancellationToken ct)
    {
        facts.Add(FactRegistration.Of(e.MessageId, "AccountCreated", e.ActorType.ToString(), null, e.OccurredOnUtc));
        return Task.CompletedTask;
    }
}

public sealed class AccountApprovedProjector(IFactStore facts) : IFactProjector<AccountApprovedIntegrationEvent>
{
    public Task ProjectAsync(AccountApprovedIntegrationEvent e, CancellationToken ct)
    {
        facts.Add(FactRegistration.Of(e.MessageId, "AccountApproved", e.ActorType.ToString(), null, e.OccurredOnUtc));
        return Task.CompletedTask;
    }
}

public sealed class AccountSuspendedProjector(IFactStore facts) : IFactProjector<AccountSuspendedIntegrationEvent>
{
    public Task ProjectAsync(AccountSuspendedIntegrationEvent e, CancellationToken ct)
    {
        facts.Add(FactRegistration.Of(e.MessageId, "AccountSuspended", e.ActorType.ToString(), null, e.OccurredOnUtc));
        return Task.CompletedTask;
    }
}

/// <summary>The governorate is the only geographic dimension of candidates (non-PII, BC-04 payload enrichment).</summary>
public sealed class ProfileCreatedProjector(IFactStore facts) : IFactProjector<ProfileCreatedIntegrationEvent>
{
    public Task ProjectAsync(ProfileCreatedIntegrationEvent e, CancellationToken ct)
    {
        facts.Add(FactRegistration.Of(e.MessageId, "ProfileCreated", "JobSeeker", e.Governorate, e.OccurredOnUtc));
        return Task.CompletedTask;
    }
}

public sealed class ProfileUpdatedProjector(IFactStore facts) : IFactProjector<ProfileUpdatedIntegrationEvent>
{
    public Task ProjectAsync(ProfileUpdatedIntegrationEvent e, CancellationToken ct)
    {
        facts.Add(FactRegistration.Of(e.MessageId, "ProfileUpdated", "JobSeeker", null, e.OccurredOnUtc));
        return Task.CompletedTask;
    }
}

public sealed class EmployerRegistrationApprovedProjector(IFactStore facts) : IFactProjector<EmployerRegistrationApprovedIntegrationEvent>
{
    public Task ProjectAsync(EmployerRegistrationApprovedIntegrationEvent e, CancellationToken ct)
    {
        facts.Add(FactRegistration.Of(e.MessageId, "EmployerRegistrationApproved", "Employer", null, e.OccurredOnUtc));
        return Task.CompletedTask;
    }
}

public sealed class NotificationSentProjector(IFactStore facts) : IFactProjector<NotificationSentIntegrationEvent>
{
    public async Task ProjectAsync(NotificationSentIntegrationEvent e, CancellationToken ct)
    {
        if (await facts.GetNotificationAsync(e.NotificationId, ct) is null)
        {
            facts.Add(FactNotification.Of(e.NotificationId, e.Channel, e.Category, e.Status, e.OccurredOnUtc));
        }
    }
}

/// <summary>Status updates that arrive before the notification is known are retried later by the inbox (out-of-order tolerance, foundation 9.4 rule 3).</summary>
public sealed class NotificationStatusUpdatedProjector(IFactStore facts) : IFactProjector<NotificationStatusUpdatedIntegrationEvent>
{
    public async Task ProjectAsync(NotificationStatusUpdatedIntegrationEvent e, CancellationToken ct)
    {
        var notification = await facts.GetNotificationAsync(e.NotificationStatusId, ct)
                           ?? throw new InvalidOperationException($"Notification {e.NotificationStatusId} is not known yet.");
        notification.UpdateStatus(e.ToStatus);
    }
}

public sealed class JobConfirmationSentProjector(IFactStore facts) : IFactProjector<JobConfirmationSentIntegrationEvent>
{
    public async Task ProjectAsync(JobConfirmationSentIntegrationEvent e, CancellationToken ct)
    {
        if (await facts.GetNotificationAsync(e.JobConfirmationId, ct) is null)
        {
            facts.Add(FactNotification.Of(e.JobConfirmationId, "Email", "JobConfirmation", "Sent", e.OccurredOnUtc));
        }
    }
}

public sealed class MatchScoreComputedProjector(IFactStore facts) : IFactProjector<MatchScoreComputedIntegrationEvent>
{
    public async Task ProjectAsync(MatchScoreComputedIntegrationEvent e, CancellationToken ct)
    {
        if (!await facts.MatchExistsAsync(e.MatchScoreId, ct))
        {
            facts.Add(FactMatch.ForScore(e.MatchScoreId, e.JobPostingId, e.Score, e.ConfigVersion, e.OccurredOnUtc));
        }
    }
}

public sealed class JobRecommendationComputedProjector(IFactStore facts) : IFactProjector<JobRecommendationComputedIntegrationEvent>
{
    public async Task ProjectAsync(JobRecommendationComputedIntegrationEvent e, CancellationToken ct)
    {
        if (!await facts.MatchExistsAsync(e.JobRecommendationId, ct))
        {
            facts.Add(FactMatch.Recommendation(e.JobRecommendationId, e.TopJobIds.Count, e.OccurredOnUtc));
        }
    }
}

public sealed class TalentPoolEntryCreatedProjector(IFactStore facts) : IFactProjector<TalentPoolEntryCreatedIntegrationEvent>
{
    public async Task ProjectAsync(TalentPoolEntryCreatedIntegrationEvent e, CancellationToken ct) =>
        (await facts.GetOrOpenOutcomeAsync(e.JobPostingId, e.OccurredOnUtc, ct)).Shortlist(e.OccurredOnUtc);
}

public sealed class CandidateInsightComputedProjector(IFactStore facts) : IFactProjector<CandidateInsightComputedIntegrationEvent>
{
    public async Task ProjectAsync(CandidateInsightComputedIntegrationEvent e, CancellationToken ct) =>
        (await facts.GetOrOpenOutcomeAsync(e.JobPostingId, e.OccurredOnUtc, ct)).FollowUp(e.FitScore, e.OccurredOnUtc);
}
