using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>Local replica fed by BC-03's AccountApproved for an ExternalJobSite actor (foundation section 9.5: prefer replication over a
/// live call). Plain record, not an aggregate root — it carries no behaviour, only the fact that a partner account exists.</summary>
public sealed class KnownPartnerAccount
{
    private KnownPartnerAccount()
    {
    }

    public KnownPartnerAccount(Guid accountId, DateTime activeSinceUtc)
    {
        AccountId = accountId;
        ActiveSinceUtc = activeSinceUtc;
    }

    public Guid AccountId { get; private set; }
    public DateTime ActiveSinceUtc { get; private set; }
}

/// <summary>Local replica of a BC-03 API credential (§5.2 `RecordPartnerCredentialHandler`), fed by ApiCredentialCreated, so requests can be
/// tied to an active credential (precondition of US-3.1.3-03/05) without a live call. Plain record: idempotent upsert by ApiCredentialId,
/// ignoring an older or equal aggregateVersion.</summary>
public sealed class PartnerCredential
{
    private PartnerCredential()
    {
    }

    public PartnerCredential(Guid apiCredentialId, Guid accountId, DateTime expiresAtUtc, long eventVersion)
    {
        ApiCredentialId = apiCredentialId;
        AccountId = accountId;
        ExpiresAtUtc = expiresAtUtc;
        LastEventVersion = eventVersion;
    }

    public Guid ApiCredentialId { get; private set; }
    public Guid AccountId { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public long LastEventVersion { get; private set; }

    public bool IsActive(DateTime nowUtc) => ExpiresAtUtc > nowUtc;

    public void Update(DateTime expiresAtUtc, long eventVersion)
    {
        if (eventVersion <= LastEventVersion)
        {
            return;
        }

        ExpiresAtUtc = expiresAtUtc;
        LastEventVersion = eventVersion;
    }
}

/// <summary>Records one partner view of the published schema documentation and raises ApiSchemaDocumentationViewed on every view
/// (handover Q-03: the event catalogue publishes it although 3.1.3-12 AC-03 called it "not applicable" — this implementation follows the
/// catalogue and publishes).</summary>
public sealed class ApiSchemaAccessLog : AggregateRoot<Guid>
{
    private ApiSchemaAccessLog()
    {
    }

    public string ApiVersion { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public DateTime ViewedAtUtc { get; private set; }

    public static ApiSchemaAccessLog Record(Guid id, string apiVersion, Guid actorId, DateTime nowUtc)
    {
        var log = new ApiSchemaAccessLog { Id = id, ApiVersion = apiVersion, ActorId = actorId, ViewedAtUtc = nowUtc };
        log.Raise(new ApiSchemaDocumentationViewedDomainEvent(id, actorId, apiVersion, nowUtc));
        return log;
    }
}
