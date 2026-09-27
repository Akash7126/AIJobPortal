namespace JobPlatform.EmployerOnboarding.Domain;

/// <summary>Local replica fed by BC-03's AccountApproved (foundation section 9.5: prefer replication over a live call). Plain record, not an
/// aggregate root - it carries no behaviour or invariants, only the fact that an employer account exists and is active.</summary>
public sealed class KnownAccount
{
    private KnownAccount()
    {
    }

    public KnownAccount(Guid accountId, DateTime activeSinceUtc)
    {
        AccountId = accountId;
        ActiveSinceUtc = activeSinceUtc;
    }

    public Guid AccountId { get; private set; }

    public DateTime ActiveSinceUtc { get; private set; }
}
