using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.SharedKernel.Messaging;

public abstract record IntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1) : IIntegrationEvent
{
    public abstract string EventType { get; }
    public abstract string Exchange { get; }
    public abstract string RoutingKey { get; }
    public abstract string Producer { get; }
}

/// <summary>Context a domain-event mapper needs to build an integration event.</summary>
public sealed record DomainEventContext(string AggregateId, long AggregateVersion, Guid CorrelationId, Guid? CausationId);

/// <summary>Wire form of a message: headers + JSON body, exchange and routing key already resolved.</summary>
public sealed record MessageEnvelope(
    Guid MessageId,
    string Type,
    int Version,
    string Exchange,
    string RoutingKey,
    string Producer,
    Guid CorrelationId,
    Guid? CausationId,
    DateTime OccurredOnUtc,
    string Payload)
{
    public const string ContentType = "application/json";

    public IReadOnlyDictionary<string, string> ToHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            [MessagingHeaders.MessageId] = MessageId.ToString(),
            [MessagingHeaders.Type] = Type,
            [MessagingHeaders.Version] = Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MessagingHeaders.CorrelationId] = CorrelationId.ToString(),
            [MessagingHeaders.OccurredOn] = OccurredOnUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            [MessagingHeaders.Producer] = Producer,
            [MessagingHeaders.ContentType] = ContentType
        };
        if (CausationId is { } causation)
        {
            headers[MessagingHeaders.CausationId] = causation.ToString();
        }

        return headers;
    }
}

public static class MessagingHeaders
{
    public const string MessageId = "message-id";
    public const string Type = "type";
    public const string Version = "version";
    public const string CorrelationId = "correlation-id";
    public const string CausationId = "causation-id";
    public const string OccurredOn = "occurred-on";
    public const string Producer = "producer";
    public const string ContentType = "content-type";

    /// <summary>W3C trace context, so a trace continues from the publishing request through the broker (foundation section 12).</summary>
    public const string TraceParent = "traceparent";
}

public static class ExchangeNames
{
    public const string AccountIdentity = "jobplatform.account-identity.events";
    public const string DeadLetter = "jobplatform.dlx";
    public const string AuditRecords = "jobplatform.audit.records";
    public const string GovernmentIntegration = "jobplatform.government-integration.events";
    public const string ExternalIntegration = "jobplatform.external-integration.events";
    public const string JobSeekerProfile = "jobplatform.job-seeker-profile.events";
    public const string EmployerOnboarding = "jobplatform.employer-onboarding.events";
    public const string HelpContent = "jobplatform.help-content.events";
    public const string PlatformAdministration = "jobplatform.platform-administration.events";
    public const string JobPosting = "jobplatform.job-posting.events";
    public const string AiMatching = "jobplatform.ai-matching.events";
    public const string CandidateSourcing = "jobplatform.candidate-sourcing.events";
    public const string Reporting = "jobplatform.reporting.events";
    public const string Notification = "jobplatform.notification.events";
}

public static class RoutingKeys
{
    public const string AccountCreated = "account.created.v1";
    public const string AccountApproved = "account.approved.v1";
    public const string AccountSuspended = "account.suspended.v1";
    public const string ApiCredentialCreated = "api-credential.created.v1";
    public const string UserAccountApproved = "user-account.approved.v1";

    // BC-01 Government Integration
    public const string EmployerVerificationApproved = "employer-verification.approved.v1";
    public const string GovernmentVerificationDataImported = "government-verification-data.imported.v1";
    public const string EducationalCredentialVerificationImported = "educational-credential-verification.imported.v1";
    public const string IdentityVerificationDataImported = "identity-verification-data.imported.v1";
    public const string LegacyDataImported = "legacy-data.imported.v1";
    public const string DataQualityUpdated = "data-quality.updated.v1";

    // BC-02 External Integration
    public const string JobDataImported = "job-data.imported.v1";
    public const string JobPostAttributionUpdated = "job-post-attribution.updated.v1";
    public const string JobDataMappingUpdated = "job-data-mapping.updated.v1";
    public const string ExternalJobSiteIntegrationSupported = "external-job-site-integration.supported.v1";
    public const string AttributionVisibilityConfigured = "attribution-visibility.configured.v1";
    public const string ApiSchemaDocumentationViewed = "api-schema-documentation.viewed.v1";

    // BC-04 Job Seeker Profile
    public const string ProfileCreated = "profile.created.v1";
    public const string ProfileUpdated = "profile.updated.v1";
    public const string ResumeCreated = "resume.created.v1";

    // BC-05 Employer Onboarding
    public const string EmployerRegistrationApproved = "employer-registration.approved.v1";
    public const string CompanyMediaAndDocumentCreated = "company-media-and-document.created.v1";

    // BC-06 Help Content
    public const string NewsArticleCreated = "news-article.created.v1";
    public const string NewsArticlePublished = "news-article.published.v1";
    public const string NewsArticleArchived = "news-article.archived.v1";
    public const string HelpContentUpdated = "help-content.updated.v1";

    // BC-08 Platform Administration
    public const string PlatformEntityRecordCreated = "platform-entity-record.created.v1";
    public const string PlatformTaxonomyUpdated = "platform-taxonomy.updated.v1";
    public const string JobOfferingSuspended = "job-offering.suspended.v1";
    public const string ReferenceFileUpdated = "reference-file.updated.v1";
    public const string SystemSettingChanged = "system-setting.changed.v1";

    // BC-09 Job Posting
    public const string JobPostingCreated = "job-posting.created.v1";
    public const string JobPostingUpdated = "job-posting.updated.v1";
    public const string JobPostingRenewed = "job-posting.renewed.v1";
    public const string JobPostingStatusUpdated = "job-posting-status.updated.v1";
    public const string FavoriteJobListCreated = "favorite-job-list.created.v1";
    public const string InterestedListEntryCreated = "interested-list-entry.created.v1";
    public const string SavedSearchMatched = "saved-search.matched.v1";

    // BC-10 AI Matching
    public const string MatchScoreComputed = "match-score.computed.v1";
    public const string ResumeParsedDataComputed = "resume-parsed-data.computed.v1";
    public const string SkillStandardizationUpdated = "skill-standardization.updated.v1";
    public const string ParsedProfileDataUpdated = "parsed-profile-data.updated.v1";
    public const string JobRecommendationComputed = "job-recommendation.computed.v1";

    // BC-11 Candidate Sourcing
    public const string TalentPoolEntryCreated = "talent-pool-entry.created.v1";
    public const string CandidateInsightComputed = "candidate-insight.computed.v1";

    // BC-12 Reporting
    public const string PerformanceAlertRaised = "performance-alert.raised.v1";
    public const string ReportDistributionRequested = "report-distribution.requested.v1";

    // BC-13 Notification
    public const string NotificationSent = "notification.sent.v1";
    public const string NotificationStatusUpdated = "notification-status.updated.v1";
    public const string JobConfirmationSent = "job-confirmation.sent.v1";
}

public static class BoundedContextSlugs
{
    public const string AccountIdentity = "account-identity";
    public const string GovernmentIntegration = "government-integration";
    public const string ExternalIntegration = "external-integration";
    public const string JobSeekerProfile = "job-seeker-profile";
    public const string EmployerOnboarding = "employer-onboarding";
    public const string HelpContent = "help-content";
    public const string AuditLogging = "audit-logging";
    public const string PlatformAdministration = "platform-administration";
    public const string JobPosting = "job-posting";
    public const string AiMatching = "ai-matching";
    public const string CandidateSourcing = "candidate-sourcing";
    public const string Reporting = "reporting";
    public const string Notification = "notification";
}

/// <summary>Serializer settings for integration-event payloads: camelCase JSON, string enums.</summary>
public static class IntegrationJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (!typeof(IIntegrationEvent).IsAssignableFrom(typeInfo.Type))
            {
                return;
            }

            // Routing metadata is transport information (headers), not payload.
            for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                if (typeInfo.Properties[i].Name is "eventType" or "exchange" or "routingKey" or "producer")
                {
                    typeInfo.Properties.RemoveAt(i);
                }
            }
        });
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false,
            TypeInfoResolver = resolver
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static string Serialize(IIntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);

    public static MessageEnvelope ToEnvelope(IIntegrationEvent e) =>
        new(e.MessageId, e.EventType, e.Version, e.Exchange, e.RoutingKey, e.Producer, e.CorrelationId, e.CausationId, e.OccurredOnUtc, Serialize(e));
}
