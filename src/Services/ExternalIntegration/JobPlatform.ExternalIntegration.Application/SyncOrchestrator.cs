namespace JobPlatform.ExternalIntegration.Application;

/// <summary>The partner feed did not respond within the retry budget (handover 4.2: 30s timeout × 3 retries). Not a BusinessRuleViolationException
/// because it maps to 502 (ErrorType.External), which the shared rule-to-error mapping does not cover.</summary>
public sealed class PartnerUpstreamTimeoutException : Exception
{
    public PartnerUpstreamTimeoutException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
