using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.SharedKernel.Domain;

public abstract record DomainEvent(DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>How a broken rule is reported over HTTP (mapped by the Api layer, see foundation section 7).</summary>
public enum BusinessRuleKind
{
    /// <summary>422 - well-formed request refused in the current state.</summary>
    BusinessRule,
    /// <summary>409 - state or uniqueness conflict.</summary>
    Conflict,
    /// <summary>403 - caller may not perform this.</summary>
    Forbidden,
    /// <summary>401 - caller not (or no longer) authenticated.</summary>
    Unauthorized,
    /// <summary>429 - throttled or locked.</summary>
    RateLimited,
    /// <summary>400 - a value violates a domain policy (e.g. password policy).</summary>
    InvalidInput
}

public sealed class BusinessRule : IBusinessRule
{
    private readonly bool _broken;

    public BusinessRule(string code, string message, bool isBroken, string? externalCode = null,
        BusinessRuleKind kind = BusinessRuleKind.BusinessRule)
    {
        Code = code;
        Message = message;
        _broken = isBroken;
        ExternalCode = externalCode;
        Kind = kind;
    }

    public string Code { get; }
    public string Message { get; }
    public string? ExternalCode { get; }
    public BusinessRuleKind Kind { get; }
    public bool IsBroken() => _broken;
}

public sealed class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(IBusinessRule rule, IReadOnlyDictionary<string, object?>? args = null)
        : this(rule.Code, rule.Message, rule.ExternalCode, rule.Kind, args)
    {
    }

    public BusinessRuleViolationException(string code, string message, string? externalCode = null,
        BusinessRuleKind kind = BusinessRuleKind.BusinessRule, IReadOnlyDictionary<string, object?>? args = null)
        : base(message)
    {
        Code = code;
        ExternalCode = externalCode;
        Kind = kind;
        Args = args ?? new Dictionary<string, object?>();
    }

    public string Code { get; }
    public string? ExternalCode { get; }
    public BusinessRuleKind Kind { get; }
    public IReadOnlyDictionary<string, object?> Args { get; }
}

public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected static void Check(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleViolationException(rule);
        }
    }

    public bool Equals(Entity<TId>? other) =>
        other is not null && (ReferenceEquals(this, other) || (GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id)));

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : notnull
{
    private readonly List<IDomainEvent> _events = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    /// <summary>Optimistic concurrency token (rowversion on SQL Server).</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>Monotonic per-aggregate counter, incremented once per raised domain event. Published as aggregateVersion.</summary>
    public long Version { get; private set; }

    public string AggregateId => Id.ToString() ?? string.Empty;

    protected void Raise(IDomainEvent domainEvent)
    {
        Version++;
        _events.Add(domainEvent);
    }

    public void ClearDomainEvents() => _events.Clear();
}

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null && GetType() == other.GetType() && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (hash, component) => HashCode.Combine(hash, component));
}
