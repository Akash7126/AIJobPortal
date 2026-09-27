using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.Audit;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.AuditLogging.Application.Ingestion;

// Inbox handlers of BC-07 (handover 5.2). Each is an application use case: dedupe, record the immutable entry, update the projections.
// They are idempotent (INV-01 plus natural idempotency of the projections) and never call another BC.

internal static class Src
{
    public const string Identity = BoundedContextSlugs.AccountIdentity;
    public const string Profile = BoundedContextSlugs.JobSeekerProfile;
    public const string Employer = BoundedContextSlugs.EmployerOnboarding;
    public const string Government = BoundedContextSlugs.GovernmentIntegration;
    public const string External = BoundedContextSlugs.ExternalIntegration;
    public const string Posting = BoundedContextSlugs.JobPosting;
    public const string Sourcing = BoundedContextSlugs.CandidateSourcing;
    public const string Notification = BoundedContextSlugs.Notification;
    public const string Platform = BoundedContextSlugs.PlatformAdministration;

    public static Dictionary<string, string> Details(params (string Key, string? Value)[] items) =>
        items.Where(i => i.Value is not null).ToDictionary(i => i.Key, i => i.Value!);
}

// ---------------------------------------------------------------------- BC-03 Account Identity

public sealed class AccountCreatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<AccountCreatedIntegrationEvent>
{
    public Task Handle(AccountCreatedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Identity, e.MessageId, AuditCategory.Access, e.OccurredOnUtc, e.ActorId, e.ActorType.ToString(), "Account", e.AccountId.ToString(),
            OwnerScope.AdminOnly, "AccountCreated", AuditOutcome.Success, null, Src.Details(("actorType", e.ActorType.ToString())), ct);
}

public sealed class AccountApprovedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<AccountApprovedIntegrationEvent>
{
    public Task Handle(AccountApprovedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Identity, e.MessageId, AuditCategory.Access, e.OccurredOnUtc, e.ActorId, e.ActorType.ToString(), "Account", e.AccountId.ToString(),
            OwnerScope.AdminOnly, "AccountApproved", AuditOutcome.Success, null, Src.Details(("actorType", e.ActorType.ToString())), ct);
}

public sealed class UserAccountApprovedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<UserAccountApprovedIntegrationEvent>
{
    public Task Handle(UserAccountApprovedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Identity, e.MessageId, AuditCategory.AdminAction, e.OccurredOnUtc, e.ActorId, "Administrator", "UserAccount",
            e.UserAccountId.ToString(), OwnerScope.AdminOnly, "UserAccountStandingChanged", AuditOutcome.Success, null,
            Src.Details(("from", e.FromStanding), ("to", e.ToStanding)), ct);
}

public sealed class ApiCredentialCreatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<ApiCredentialCreatedIntegrationEvent>
{
    public Task Handle(ApiCredentialCreatedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Identity, e.MessageId, AuditCategory.AdminAction, e.OccurredOnUtc, e.ActorId, null, "ApiCredential", e.ApiCredentialId.ToString(),
            OwnerScope.AdminOnly, "ApiCredentialCreated", AuditOutcome.Success, null,
            Src.Details(("partnerAccountId", e.AccountId.ToString()), ("expiresAtUtc", e.ExpiresAtUtc.ToString("O"))), ct);
}

// ---------------------------------------------------------------------- BC-04 / BC-05 / BC-01

public sealed class ProfileCreatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<ProfileCreatedIntegrationEvent>
{
    public Task Handle(ProfileCreatedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Profile, e.MessageId, AuditCategory.Profile, e.OccurredOnUtc, e.ActorId, "JobSeeker", "Profile", e.ProfileId.ToString(),
            OwnerScope.AdminOnly, "ProfileCreated", AuditOutcome.Success, null, Src.Details(("ownerAccountId", e.OwnerAccountId.ToString()), ("governorate", e.Governorate)), ct);
}

public sealed class EmployerRegistrationApprovedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<EmployerRegistrationApprovedIntegrationEvent>
{
    public async Task Handle(EmployerRegistrationApprovedIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.RecordAsync(Src.Employer, e.MessageId, AuditCategory.AdminAction, e.OccurredOnUtc, e.ActorId, "Administrator", "EmployerRegistration",
            e.EmployerRegistrationId.ToString(), OwnerScope.AdminOnly, "EmployerRegistrationApproved", AuditOutcome.Success, null,
            Src.Details(("employerAccountId", e.EmployerAccountId.ToString())), ct);
        (await ingestion.DashboardAsync(e.EmployerAccountId, e.OccurredOnUtc, ct)).MarkRegistrationApproved(e.OccurredOnUtc);
    }
}

public sealed class EmployerVerificationApprovedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<EmployerVerificationApprovedIntegrationEvent>
{
    public Task Handle(EmployerVerificationApprovedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Government, e.MessageId, AuditCategory.GovernmentExchange, e.OccurredOnUtc, e.ActorId, null, "EmployerVerification",
            e.EmployerVerificationId.ToString(), OwnerScope.AdminOnly, "EmployerVerificationApproved", AuditOutcome.Success, null,
            Src.Details(("employerAccountId", e.EmployerAccountId.ToString()), ("method", e.Method)), ct);
}

public sealed class GovernmentVerificationDataImportedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<GovernmentVerificationDataImportedIntegrationEvent>
{
    public Task Handle(GovernmentVerificationDataImportedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Government, e.MessageId, AuditCategory.GovernmentExchange, e.OccurredOnUtc, null, "System", "GovernmentVerificationData",
            e.GovernmentVerificationDataId.ToString(), OwnerScope.AdminOnly, "GovernmentQueryCompleted",
            e.Outcome == "Verified" ? AuditOutcome.Success : AuditOutcome.Failure, null,
            Src.Details(("subjectType", e.SubjectType), ("subjectId", e.SubjectId.ToString()), ("outcome", e.Outcome)), ct);
}

// ---------------------------------------------------------------------- BC-02 External Integration

public sealed class JobDataImportedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<JobDataImportedIntegrationEvent>
{
    public async Task Handle(JobDataImportedIntegrationEvent e, CancellationToken ct)
    {
        var partner = OwnerScope.Of(OwnerType.Partner, e.ActorId);
        var action = e.IsUpdate ? "JobUpdateImported" : "JobImported";
        var details = Src.Details(("sourcePlatformId", e.SourcePlatformId.ToString()), ("platformJobId", e.PlatformJobId), ("sourceJobId", e.SourceJobId));
        await ingestion.RecordAsync(Src.External, e.MessageId, AuditCategory.Submission, e.OccurredOnUtc, e.ActorId, "ExternalJobSite", "Job", e.PlatformJobId,
            partner, action, AuditOutcome.Success, null, details, ct);
        await ingestion.RecordAsync(Src.External, e.MessageId, AuditCategory.JobAudit, e.OccurredOnUtc, e.ActorId, "ExternalJobSite", "Job", e.PlatformJobId,
            OwnerScope.AdminOnly, action, AuditOutcome.Success, null, details, ct);
        await ingestion.MarkSyncedAsync(e.PlatformJobId, e.SourcePlatformId, e.ActorId, e.OccurredOnUtc, e.AggregateVersion, ct);
        if (!e.IsUpdate)
        {
            await ingestion.CountSubmissionAsync(e.ActorId, e.OccurredOnUtc, ct);
        }
    }
}

public sealed class JobPostAttributionUpdatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<JobPostAttributionUpdatedIntegrationEvent>
{
    private static readonly string[] Terminal = { "Closed", "Deactivated", "Deleted" };

    public async Task Handle(JobPostAttributionUpdatedIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.RecordAsync(Src.External, e.MessageId, AuditCategory.JobAudit, e.OccurredOnUtc, e.ActorId, "ExternalJobSite", "Job", e.PlatformJobId,
            OwnerScope.AdminOnly, "JobPostAttributionUpdated", AuditOutcome.Success, null, Src.Details(("from", e.FromStatus), ("to", e.ToStatus)), ct);
        if (Terminal.Contains(e.ToStatus, StringComparer.OrdinalIgnoreCase))
        {
            await ingestion.ArchiveSyncAsync(e.PlatformJobId, e.OccurredOnUtc, e.AggregateVersion, ct);
        }
    }
}

// ---------------------------------------------------------------------- BC-09 Job Posting

public sealed class JobPostingCreatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<JobPostingCreatedIntegrationEvent>
{
    public async Task Handle(JobPostingCreatedIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.RecordJobStatusAsync(e.MessageId, e.JobPostingId, e.EmployerAccountId, null, e.Status, null, e.OccurredOnUtc, ct);
        (await ingestion.DashboardAsync(e.EmployerAccountId, e.OccurredOnUtc, ct)).TrackPosting(e.JobPostingId, e.Title, e.Status, e.OccurredOnUtc);
    }
}

public sealed class JobPostingUpdatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<JobPostingUpdatedIntegrationEvent>
{
    public async Task Handle(JobPostingUpdatedIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.RecordAsync(Src.Posting, e.MessageId, AuditCategory.JobStatus, e.OccurredOnUtc, e.ActorId, null, "JobPosting", e.JobPostingId.ToString(),
            OwnerScope.Of(OwnerType.Employer, e.EmployerAccountId), "JobPostingUpdated", AuditOutcome.Success, null,
            Src.Details(("changedFields", string.Join(',', e.ChangedFields))), ct);
        if (!string.Equals(e.FromStatus, e.ToStatus, StringComparison.Ordinal))
        {
            await ingestion.RecordJobStatusAsync(e.MessageId, e.JobPostingId, e.EmployerAccountId, e.FromStatus, e.ToStatus, null, e.OccurredOnUtc, ct);
            (await ingestion.DashboardAsync(e.EmployerAccountId, e.OccurredOnUtc, ct)).TrackPosting(e.JobPostingId, null, e.ToStatus, e.OccurredOnUtc);
        }
    }
}

public sealed class JobPostingStatusUpdatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<JobPostingStatusUpdatedIntegrationEvent>
{
    public async Task Handle(JobPostingStatusUpdatedIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.RecordAsync(Src.Posting, e.MessageId, AuditCategory.JobStatus, e.OccurredOnUtc, e.ActorId, null, "JobPosting", e.JobPostingId.ToString(),
            OwnerScope.Of(OwnerType.Employer, e.EmployerAccountId), "JobPostingStatusChanged", AuditOutcome.Success, null,
            Src.Details(("from", e.FromStatus), ("to", e.ToStatus), ("reason", e.Reason)), ct);
        await ingestion.RecordJobStatusAsync(e.MessageId, e.JobPostingId, e.EmployerAccountId, e.FromStatus, e.ToStatus, e.Reason, e.OccurredOnUtc, ct);
        (await ingestion.DashboardAsync(e.EmployerAccountId, e.OccurredOnUtc, ct)).TrackPosting(e.JobPostingId, null, e.ToStatus, e.OccurredOnUtc);
    }
}

// ---------------------------------------------------------------------- BC-11 Candidate Sourcing

public sealed class CandidateInsightComputedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<CandidateInsightComputedIntegrationEvent>
{
    public async Task Handle(CandidateInsightComputedIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.RecordAsync(Src.Sourcing, e.MessageId, AuditCategory.Insight, e.OccurredOnUtc, e.ActorId, "Employer", "CandidateInsight",
            e.CandidateInsightId.ToString(), OwnerScope.Of(OwnerType.Employer, e.EmployerAccountId), "CandidateInsightComputed", AuditOutcome.Success, null,
            Src.Details(("jobPostingId", e.JobPostingId.ToString()), ("candidateProfileId", e.CandidateProfileId.ToString())), ct);
        await ingestion.UpsertInsightAsync(e.CandidateInsightId, e.JobPostingId, e.EmployerAccountId, e.CandidateProfileId, e.Availability, e.ExpectedSalary, e.FitScore,
            e.WithheldFields, e.OccurredOnUtc, ct);
    }
}

public sealed class TalentPoolEntryCreatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<TalentPoolEntryCreatedIntegrationEvent>
{
    public async Task Handle(TalentPoolEntryCreatedIntegrationEvent e, CancellationToken ct)
    {
        var recorded = await ingestion.RecordAsync(Src.Sourcing, e.MessageId, AuditCategory.Insight, e.OccurredOnUtc, e.ActorId, "Employer", "TalentPoolEntry",
            e.TalentPoolEntryId.ToString(), OwnerScope.Of(OwnerType.Employer, e.EmployerAccountId), "TalentPoolEntryCreated", AuditOutcome.Success, null,
            Src.Details(("jobPostingId", e.JobPostingId.ToString())), ct);
        if (recorded)
        {
            (await ingestion.DashboardAsync(e.EmployerAccountId, e.OccurredOnUtc, ct)).CountShortlist(e.OccurredOnUtc);
        }
    }
}

// ---------------------------------------------------------------------- BC-13 Notification

public sealed class NotificationSentAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<NotificationSentIntegrationEvent>
{
    public async Task Handle(NotificationSentIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.RecordNotificationAsync(e.NotificationId, e.RecipientAccountId, e.Channel, e.Category, e.MaskedRecipient, e.Subject, e.Status, e.OccurredOnUtc, ct);
        var category = e.Channel switch { "Email" => AuditCategory.Email, "Sms" => AuditCategory.Sms, _ => AuditCategory.Notification };
        var scope = e.RecipientAccountId is { } id && category == AuditCategory.Notification ? OwnerScope.Of(OwnerType.User, id) : OwnerScope.AdminOnly;
        await ingestion.RecordAsync(Src.Notification, e.MessageId, category, e.OccurredOnUtc, null, "System", "Notification", e.NotificationId.ToString(), scope,
            "NotificationSent", e.Status == "Failed" ? AuditOutcome.Failure : AuditOutcome.Success, null,
            Src.Details(("channel", e.Channel), ("category", e.Category), ("status", e.Status)), ct);
    }
}

public sealed class NotificationStatusUpdatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<NotificationStatusUpdatedIntegrationEvent>
{
    public async Task Handle(NotificationStatusUpdatedIntegrationEvent e, CancellationToken ct)
    {
        await ingestion.UpdateNotificationStatusAsync(e.NotificationStatusId, e.ToStatus, e.OccurredOnUtc, ct);
        await ingestion.RecordAsync(Src.Notification, e.MessageId, AuditCategory.Notification, e.OccurredOnUtc, e.RecipientAccountId, null, "Notification",
            e.NotificationStatusId.ToString(), OwnerScope.Of(OwnerType.User, e.RecipientAccountId), "NotificationStatusChanged", AuditOutcome.Success, null,
            Src.Details(("from", e.FromStatus), ("to", e.ToStatus)), ct);
    }
}

// ---------------------------------------------------------------------- BC-08 Platform Administration

public sealed class PlatformEntityRecordCreatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<PlatformEntityRecordCreatedIntegrationEvent>
{
    public Task Handle(PlatformEntityRecordCreatedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Platform, e.MessageId, AuditCategory.AdminAction, e.OccurredOnUtc, e.ActorId, "Administrator", "PlatformEntityRecord",
            e.PlatformEntityRecordId.ToString(), OwnerScope.AdminOnly, "PlatformEntityRecordCreated", AuditOutcome.Success, null, Src.Details(("entityType", e.EntityType)), ct);
}

public sealed class PlatformTaxonomyUpdatedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<PlatformTaxonomyUpdatedIntegrationEvent>
{
    public Task Handle(PlatformTaxonomyUpdatedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Platform, e.MessageId, AuditCategory.AdminAction, e.OccurredOnUtc, e.ActorId, "Administrator", "PlatformTaxonomy",
            e.PlatformTaxonomyId.ToString(), OwnerScope.AdminOnly, "PlatformTaxonomyUpdated", AuditOutcome.Success, null,
            Src.Details(("taxonomyType", e.TaxonomyType), ("fromVersion", e.FromVersion.ToString()), ("toVersion", e.ToVersion.ToString())), ct);
}

public sealed class JobOfferingSuspendedAuditHandler(AuditIngestion ingestion) : IIntegrationEventHandler<JobOfferingSuspendedIntegrationEvent>
{
    public Task Handle(JobOfferingSuspendedIntegrationEvent e, CancellationToken ct) =>
        ingestion.RecordAsync(Src.Platform, e.MessageId, AuditCategory.AdminAction, e.OccurredOnUtc, e.ActorId, "Administrator", "JobOffering", e.JobOfferingId.ToString(),
            OwnerScope.AdminOnly, "JobOfferingSuspended", AuditOutcome.Success, null, Src.Details(("jobPostingId", e.JobPostingId.ToString())), ct);
}

// ---------------------------------------------------------------------- generic operational stream (Q-01, decision D-011)

/// <summary>
/// Operational records published by any BC on jobplatform.audit.records: API outcomes, rejected pushes, sync failures, login failures, delivery detail.
/// Free-form details are sanitised (violating keys dropped and counted in "redacted"), never rejected, so one bad producer cannot poison the queue.
/// </summary>
public sealed class AuditRecordHandler(AuditIngestion ingestion) : IIntegrationEventHandler<AuditRecordIntegrationEvent>
{
    public async Task Handle(AuditRecordIntegrationEvent e, CancellationToken ct)
    {
        if (!Enum.TryParse<AuditCategory>(e.Category, true, out var category))
        {
            throw new InvalidOperationException($"Unknown audit category '{e.Category}' from {e.SourceBc}.");
        }

        var outcome = Enum.TryParse<AuditOutcome>(e.Outcome, true, out var parsed) ? parsed : AuditOutcome.Failure;
        var details = DetailsPolicy.Sanitize(e.Details, out var removed);
        if (removed > 0)
        {
            details["redacted"] = removed.ToString();
        }

        var scope = OwnerScope.Parse(e.OwnerScope);
        var recorded = await ingestion.RecordAsync(e.SourceBc, e.MessageId, category, e.OccurredOnUtc, e.ActorId, null, e.SubjectType, e.SubjectId, scope, e.Action,
            outcome, e.Code, details, ct);

        // A sync-error record drives the SyncJobStatus machine (3.4.1-04): failure to Failed, a retry through Pending, success to Synced.
        if (recorded && category == AuditCategory.SyncError && scope.OwnerId is { } partnerId && details.TryGetValue("platformJobId", out var platformJobId))
        {
            var sourcePlatformId = details.TryGetValue("sourcePlatformId", out var sp) && Guid.TryParse(sp, out var parsedSp) ? parsedSp : Guid.Empty;
            if (outcome == AuditOutcome.Success)
            {
                await ingestion.MarkSyncedAsync(platformJobId, sourcePlatformId, partnerId, e.OccurredOnUtc, 0, ct);
            }
            else
            {
                await ingestion.MarkFailedAsync(platformJobId, sourcePlatformId, partnerId, e.Code ?? "SYNC_FAILED", details.GetValueOrDefault("retry") == "true",
                    e.OccurredOnUtc, ct);
            }
        }
    }
}
