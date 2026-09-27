using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>The partner as the origin of a job: id, display name and base URL (handover 2, "Source Platform").</summary>
public sealed class SourcePlatform : ValueObject
{
    public SourcePlatform(Guid id, string name, string baseUrl)
    {
        Id = id;
        Name = name;
        BaseUrl = baseUrl;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string BaseUrl { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
        yield return Name;
        yield return BaseUrl;
    }
}

public sealed class SyncModels : ValueObject
{
    public SyncModels(bool pullEnabled, bool pushEnabled)
    {
        PullEnabled = pullEnabled;
        PushEnabled = pushEnabled;
    }

    public bool PullEnabled { get; }
    public bool PushEnabled { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PullEnabled;
        yield return PushEnabled;
    }
}

public sealed class SyncSchedule : ValueObject
{
    public SyncSchedule(SyncMode mode, string? cron)
    {
        Mode = mode;
        Cron = cron;
    }

    public SyncMode Mode { get; }
    public string? Cron { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Mode;
        yield return Cron;
    }
}

/// <summary>The platform's standardised job-posting structure (handover 3.2) that a JobDataMapping produces and JobData.Accept validates.</summary>
public sealed class StandardJob : ValueObject
{
    public StandardJob(string title, string summary, IReadOnlyList<string> skills, string contractType, string workFormat,
        DateTime? deadlineUtc, string location, string? sourceUrl)
    {
        Title = title;
        Summary = summary;
        Skills = skills;
        ContractType = contractType;
        WorkFormat = workFormat;
        DeadlineUtc = deadlineUtc;
        Location = location;
        SourceUrl = sourceUrl;
    }

    public string Title { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Skills { get; }
    public string ContractType { get; }
    public string WorkFormat { get; }
    public DateTime? DeadlineUtc { get; }
    public string Location { get; }
    public string? SourceUrl { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Title;
        yield return Summary;
        foreach (var skill in Skills)
        {
            yield return skill;
        }

        yield return ContractType;
        yield return WorkFormat;
        yield return DeadlineUtc;
        yield return Location;
        yield return SourceUrl;
    }
}

/// <summary>One field-mapping rule of a JobDataMapping: a partner's raw field, the standard-schema field it feeds, and an optional transform.</summary>
public sealed class MappingRule : ValueObject
{
    public MappingRule(string sourceField, string targetField, MappingTransform transform)
    {
        SourceField = sourceField;
        TargetField = targetField;
        Transform = transform;
    }

    public string SourceField { get; }
    public string TargetField { get; }
    public MappingTransform Transform { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SourceField;
        yield return TargetField;
        yield return Transform;
    }
}
