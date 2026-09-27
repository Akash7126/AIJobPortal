using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.JobSeekerProfile.Domain;

/// <summary>Inbox-fed replica of BC-03 accounts (handover section 8.1 profile.KnownAccounts) - avoids a synchronous read for account activity.</summary>
public sealed class KnownAccount
{
    private KnownAccount()
    {
    }

    public Guid AccountId { get; private set; }
    public ActorType ActorType { get; private set; }
    public DateTime ActiveSinceUtc { get; private set; }
    public string Standing { get; private set; } = "Active";

    public static KnownAccount Create(Guid accountId, ActorType actorType, DateTime activeSinceUtc) =>
        new() { AccountId = accountId, ActorType = actorType, ActiveSinceUtc = activeSinceUtc, Standing = "Active" };

    public void MarkSuspended(string standing) => Standing = standing;
}

/// <summary>Idempotency guard for resume-parsing merges (handover section 8.1 profile.ProcessedParsedData): one row per processed message id.</summary>
public sealed class ProcessedParsedData
{
    private ProcessedParsedData()
    {
    }

    public Guid ResumeParsedDataId { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }

    public static ProcessedParsedData Create(Guid resumeParsedDataId, DateTime nowUtc) => new() { ResumeParsedDataId = resumeParsedDataId, ProcessedAtUtc = nowUtc };
}
