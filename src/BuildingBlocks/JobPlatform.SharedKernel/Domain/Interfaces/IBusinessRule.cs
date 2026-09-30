namespace JobPlatform.SharedKernel.Domain.Interfaces;

public interface IBusinessRule
{
    /// <summary>Stable internal code, format BC.Aggregate.RULE (e.g. AI.Account.DUPLICATE).</summary>
    string Code { get; }
    string Message { get; }
    /// <summary>Externally published error code (e.g. E-JSRPM-DUPLICATE); null when the rule has none.</summary>
    string? ExternalCode { get; }
    BusinessRuleKind Kind { get; }
    bool IsBroken();
}
