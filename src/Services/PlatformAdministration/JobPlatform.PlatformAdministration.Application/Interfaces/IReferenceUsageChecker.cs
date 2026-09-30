namespace JobPlatform.PlatformAdministration.Application.Interfaces;

/// <summary>Port to the owners of references (BC-04/09/10): which of these codes are still referenced? Adapter chosen by ReferenceUsage:Provider.</summary>
public interface IReferenceUsageChecker
{
    Task<ReferenceUsageResult> CheckAsync(string referenceType, IReadOnlyCollection<string> codes, CancellationToken ct = default);
}
