using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum ConnectionHealth
{
    Healthy,
    Degraded,
    Down
}

/// <summary>Most recent successful snapshot used when a source is unreachable (handover section 2, "Last known good").</summary>
public sealed class LastKnownGood : ValueObject
{
    public LastKnownGood(string snapshotRef, DateTime takenAtUtc)
    {
        SnapshotRef = snapshotRef;
        TakenAtUtc = takenAtUtc;
    }

    public string SnapshotRef { get; }
    public DateTime TakenAtUtc { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SnapshotRef;
        yield return TakenAtUtc;
    }
}

/// <summary>
/// Proposed (handover section 3.8): MoL/PEF (and other source) connection configuration and health, realising US-2.5-01 and US-3.4.2-02.
/// No integration event is catalogued (handover Q-06 is left open in favour of the synchronous GetGovernmentSystems read, section 6.1).
/// </summary>
public sealed class GovernmentSourceConnection : AggregateRoot<Guid>
{
    /// <summary>A-02-004: 3 retries before the connection is considered degraded.</summary>
    public const int MaxFailuresBeforeDegraded = 3;

    private GovernmentSourceConnection()
    {
    }

    public SourceSystem Source { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string AuthMethod { get; private set; } = string.Empty;
    public string CredentialRef { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public DateTime? LastSuccessfulSyncAtUtc { get; private set; }
    public LastKnownGood? LastKnownGood { get; private set; }
    public ConnectionHealth Health { get; private set; }
    public int ConsecutiveFailures { get; private set; }

    /// <summary>INV-16: only administrators may configure a connection.</summary>
    public static GovernmentSourceConnection Configure(Guid id, Actor actor, SourceSystem source, string endpoint, string authMethod,
        string credentialRef, bool enabled)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.ConnectionAdminOnly));
        return new GovernmentSourceConnection
        {
            Id = id, Source = source, Endpoint = endpoint, AuthMethod = authMethod, CredentialRef = credentialRef, Enabled = enabled,
            Health = ConnectionHealth.Healthy
        };
    }

    /// <summary>US-2.5-04 AC-04: re-applying the same configuration is idempotent - no duplicate side effects, just an upsert.</summary>
    public void Update(Actor actor, string endpoint, string authMethod, string credentialRef, bool enabled)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.ConnectionAdminOnly));
        Endpoint = endpoint;
        AuthMethod = authMethod;
        CredentialRef = credentialRef;
        Enabled = enabled;
    }

    public void RecordSyncSuccess(string snapshotRef, DateTime nowUtc)
    {
        LastSuccessfulSyncAtUtc = nowUtc;
        LastKnownGood = new LastKnownGood(snapshotRef, nowUtc);
        ConsecutiveFailures = 0;
        Health = ConnectionHealth.Healthy;
    }

    /// <summary>After 3 retries x 30s: Degraded, error E-CONSTR-UPSTREAM-TIMEOUT logged by the caller; consumers fall back to LastKnownGood.</summary>
    public void RecordSyncFailure()
    {
        ConsecutiveFailures++;
        Health = ConsecutiveFailures >= MaxFailuresBeforeDegraded ? ConnectionHealth.Degraded : Health;
    }
}
