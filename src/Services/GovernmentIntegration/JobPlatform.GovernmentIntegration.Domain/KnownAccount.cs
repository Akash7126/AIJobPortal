using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Domain;

/// <summary>Local replica fed by BC-03's AccountCreated (handover section 5.2, D-01: replaces the pipeline's synchronous read, foundation F-07).
/// Plain record, not an aggregate root: it carries no behaviour or invariants beyond the fact that an account of a given actor type exists.
/// Satisfies EmployerVerification.Request's precondition (INV-02) without EmployerVerification depending on BC-03.</summary>
public sealed class KnownAccount
{
    private KnownAccount()
    {
    }

    public KnownAccount(Guid accountId, ActorType actorType, DateTime createdAtUtc, long lastEventVersion)
    {
        AccountId = accountId;
        ActorType = actorType;
        CreatedAtUtc = createdAtUtc;
        LastEventVersion = lastEventVersion;
    }

    public Guid AccountId { get; private set; }
    public ActorType ActorType { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Highest AccountCreated.AggregateVersion applied; guards against an out-of-order or duplicate delivery regressing the replica.</summary>
    public long LastEventVersion { get; private set; }
}
