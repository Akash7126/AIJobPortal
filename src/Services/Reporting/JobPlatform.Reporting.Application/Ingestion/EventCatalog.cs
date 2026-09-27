using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.HelpContent;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.Reporting.Application.Ingestion;

/// <summary>What the generic ingestion records about an event, independent of its payload: activity type, actor and subject (all pseudonymisable ids, no PII).</summary>
public sealed record EventDescription(string ActivityType, string ActorType, Guid? ActorId, string? SubjectId);

/// <summary>
/// The 45 consumed events of handover 5.2 (K-3): one mapping from event to activity type / actor / subject. An event without an entry is a programming error
/// (the subscription list and this catalogue are asserted equal by a test).
/// </summary>
public static class EventCatalog
{
    private const string System = "System";

    public static EventDescription? Describe(IIntegrationEvent e) => e switch
    {
        // BC-01 Government Integration
        EmployerVerificationApprovedIntegrationEvent x => new(ActivityTypes.Verification, "Administrator", x.ActorId, x.EmployerVerificationId.ToString()),
        GovernmentVerificationDataImportedIntegrationEvent x => new(ActivityTypes.Verification, System, null, x.GovernmentVerificationDataId.ToString()),
        EducationalCredentialVerificationImportedIntegrationEvent x => new(ActivityTypes.Verification, System, null, x.EducationalCredentialVerificationId.ToString()),
        IdentityVerificationDataImportedIntegrationEvent x => new(ActivityTypes.Verification, System, null, x.IdentityVerificationDataId.ToString()),
        LegacyDataImportedIntegrationEvent x => new(ActivityTypes.Verification, System, null, x.LegacyDataId.ToString()),
        DataQualityUpdatedIntegrationEvent x => new(ActivityTypes.Verification, System, null, x.DataQualityId.ToString()),
        // BC-02 External Integration
        JobDataImportedIntegrationEvent x => new(ActivityTypes.Integration, "ExternalJobSite", x.ActorId, x.JobDataId.ToString()),
        JobPostAttributionUpdatedIntegrationEvent x => new(ActivityTypes.Integration, "ExternalJobSite", x.ActorId, x.JobPostAttributionId.ToString()),
        JobDataMappingUpdatedIntegrationEvent x => new(ActivityTypes.Integration, "ExternalJobSite", x.ActorId, x.JobDataMappingId.ToString()),
        ExternalJobSiteIntegrationSupportedIntegrationEvent x => new(ActivityTypes.Integration, "ExternalJobSite", x.SourcePlatformId, x.ExternalJobSiteIntegrationId.ToString()),
        AttributionVisibilityConfiguredIntegrationEvent x => new(ActivityTypes.Integration, "ExternalJobSite", x.ActorId, x.AttributionVisibilityId.ToString()),
        ApiSchemaDocumentationViewedIntegrationEvent x => new(ActivityTypes.Integration, "ExternalJobSite", x.ActorId, x.ApiSchemaDocumentationId.ToString()),
        // BC-03 Account Identity
        AccountCreatedIntegrationEvent x => new(ActivityTypes.Registration, x.ActorType.ToString(), x.ActorId, x.AccountId.ToString()),
        AccountApprovedIntegrationEvent x => new(ActivityTypes.Approval, x.ActorType.ToString(), x.ActorId, x.AccountId.ToString()),
        AccountSuspendedIntegrationEvent x => new(ActivityTypes.Administration, x.ActorType.ToString(), x.ActorId, x.AccountId.ToString()),
        ApiCredentialCreatedIntegrationEvent x => new(ActivityTypes.Administration, "Administrator", x.ActorId, x.ApiCredentialId.ToString()),
        UserAccountApprovedIntegrationEvent x => new(ActivityTypes.Approval, "Administrator", x.ActorId, x.UserAccountId.ToString()),
        // BC-04 Job Seeker Profile
        ProfileCreatedIntegrationEvent x => new(ActivityTypes.Profile, "JobSeeker", x.ActorId, x.ProfileId.ToString()),
        ProfileUpdatedIntegrationEvent x => new(ActivityTypes.Profile, "JobSeeker", x.ActorId, x.ProfileId.ToString()),
        ResumeCreatedIntegrationEvent x => new(ActivityTypes.Resume, "JobSeeker", x.ActorId, x.ResumeId.ToString()),
        // BC-05 Employer Onboarding
        EmployerRegistrationApprovedIntegrationEvent x => new(ActivityTypes.Approval, "Administrator", x.ActorId, x.EmployerRegistrationId.ToString()),
        CompanyMediaAndDocumentCreatedIntegrationEvent x => new(ActivityTypes.Profile, "Employer", x.ActorId, x.CompanyMediaAndDocumentId.ToString()),
        // BC-06 Help Content
        NewsArticleCreatedIntegrationEvent x => new(ActivityTypes.Content, "Administrator", x.ActorId, x.NewsArticleId.ToString()),
        NewsArticlePublishedIntegrationEvent x => new(ActivityTypes.Content, "Administrator", x.ActorId, x.NewsArticleId.ToString()),
        NewsArticleArchivedIntegrationEvent x => new(ActivityTypes.Content, "Administrator", x.ActorId, x.NewsArticleId.ToString()),
        HelpContentUpdatedIntegrationEvent x => new(ActivityTypes.Content, "Administrator", x.ActorId, x.HelpContentId.ToString()),
        // BC-08 Platform Administration
        PlatformEntityRecordCreatedIntegrationEvent x => new(ActivityTypes.Administration, "Administrator", x.ActorId, x.PlatformEntityRecordId.ToString()),
        PlatformTaxonomyUpdatedIntegrationEvent x => new(ActivityTypes.Administration, "Administrator", x.ActorId, x.PlatformTaxonomyId.ToString()),
        JobOfferingSuspendedIntegrationEvent x => new(ActivityTypes.Administration, "Administrator", x.ActorId, x.JobOfferingId.ToString()),
        // BC-09 Job Posting
        JobPostingCreatedIntegrationEvent x => new(ActivityTypes.JobPosting, x.Source == "External" ? "ExternalJobSite" : "Employer", x.ActorId, x.JobPostingId.ToString()),
        JobPostingUpdatedIntegrationEvent x => new(ActivityTypes.JobPosting, "Employer", x.ActorId, x.JobPostingId.ToString()),
        JobPostingRenewedIntegrationEvent x => new(ActivityTypes.JobPosting, "Employer", x.ActorId, x.JobPostingId.ToString()),
        JobPostingStatusUpdatedIntegrationEvent x => new(ActivityTypes.JobPosting, "Employer", x.ActorId, x.JobPostingId.ToString()),
        FavoriteJobListCreatedIntegrationEvent x => new(ActivityTypes.Interaction, "JobSeeker", x.ActorId, x.JobPostingId.ToString()),
        InterestedListEntryCreatedIntegrationEvent x => new(ActivityTypes.Interaction, "JobSeeker", x.ActorId, x.InterestedListEntryId.ToString()),
        // BC-10 AI Matching
        MatchScoreComputedIntegrationEvent x => new(ActivityTypes.Matching, System, null, x.MatchScoreId.ToString()),
        JobRecommendationComputedIntegrationEvent x => new(ActivityTypes.Matching, "JobSeeker", x.ActorId, x.JobRecommendationId.ToString()),
        ResumeParsedDataComputedIntegrationEvent x => new(ActivityTypes.Resume, System, null, x.ResumeParsedDataId.ToString()),
        ParsedProfileDataUpdatedIntegrationEvent x => new(ActivityTypes.Resume, "JobSeeker", x.ActorId, x.ParsedProfileDataId.ToString()),
        SkillStandardizationUpdatedIntegrationEvent x => new(ActivityTypes.Matching, System, null, x.SkillStandardizationId.ToString()),
        // BC-11 Candidate Sourcing
        TalentPoolEntryCreatedIntegrationEvent x => new(ActivityTypes.CandidateSearch, "Employer", x.ActorId, x.TalentPoolEntryId.ToString()),
        CandidateInsightComputedIntegrationEvent x => new(ActivityTypes.CandidateSearch, "Employer", x.ActorId, x.CandidateInsightId.ToString()),
        // BC-13 Notification
        NotificationSentIntegrationEvent x => new(ActivityTypes.Notification, System, null, x.NotificationId.ToString()),
        NotificationStatusUpdatedIntegrationEvent x => new(ActivityTypes.Notification, System, null, x.NotificationStatusId.ToString()),
        JobConfirmationSentIntegrationEvent x => new(ActivityTypes.Notification, System, x.ActorId, x.JobConfirmationId.ToString()),
        _ => null
    };

    /// <summary>The event types this BC ingests, in the order of handover 5.2 (45).</summary>
    public static readonly IReadOnlyList<Type> ConsumedEvents = new[]
    {
        typeof(EmployerVerificationApprovedIntegrationEvent), typeof(GovernmentVerificationDataImportedIntegrationEvent),
        typeof(EducationalCredentialVerificationImportedIntegrationEvent), typeof(IdentityVerificationDataImportedIntegrationEvent),
        typeof(LegacyDataImportedIntegrationEvent), typeof(DataQualityUpdatedIntegrationEvent),
        typeof(JobDataImportedIntegrationEvent), typeof(JobPostAttributionUpdatedIntegrationEvent), typeof(JobDataMappingUpdatedIntegrationEvent),
        typeof(ExternalJobSiteIntegrationSupportedIntegrationEvent), typeof(AttributionVisibilityConfiguredIntegrationEvent),
        typeof(ApiSchemaDocumentationViewedIntegrationEvent),
        typeof(AccountCreatedIntegrationEvent), typeof(AccountApprovedIntegrationEvent), typeof(AccountSuspendedIntegrationEvent),
        typeof(ApiCredentialCreatedIntegrationEvent), typeof(UserAccountApprovedIntegrationEvent),
        typeof(ProfileCreatedIntegrationEvent), typeof(ProfileUpdatedIntegrationEvent), typeof(ResumeCreatedIntegrationEvent),
        typeof(EmployerRegistrationApprovedIntegrationEvent), typeof(CompanyMediaAndDocumentCreatedIntegrationEvent),
        typeof(NewsArticleCreatedIntegrationEvent), typeof(NewsArticlePublishedIntegrationEvent), typeof(NewsArticleArchivedIntegrationEvent),
        typeof(HelpContentUpdatedIntegrationEvent),
        typeof(PlatformEntityRecordCreatedIntegrationEvent), typeof(PlatformTaxonomyUpdatedIntegrationEvent), typeof(JobOfferingSuspendedIntegrationEvent),
        typeof(JobPostingCreatedIntegrationEvent), typeof(JobPostingUpdatedIntegrationEvent), typeof(JobPostingRenewedIntegrationEvent),
        typeof(JobPostingStatusUpdatedIntegrationEvent), typeof(FavoriteJobListCreatedIntegrationEvent), typeof(InterestedListEntryCreatedIntegrationEvent),
        typeof(MatchScoreComputedIntegrationEvent), typeof(JobRecommendationComputedIntegrationEvent), typeof(ResumeParsedDataComputedIntegrationEvent),
        typeof(ParsedProfileDataUpdatedIntegrationEvent), typeof(SkillStandardizationUpdatedIntegrationEvent),
        typeof(TalentPoolEntryCreatedIntegrationEvent), typeof(CandidateInsightComputedIntegrationEvent),
        typeof(NotificationSentIntegrationEvent), typeof(NotificationStatusUpdatedIntegrationEvent), typeof(JobConfirmationSentIntegrationEvent)
    };
}
