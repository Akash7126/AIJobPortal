using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Interfaces;

/// <summary>Common shape of a "check this subject" call against a government-verification-data source (MoL, PEF or a government database).</summary>
public interface IGovernmentVerificationSourceClient
{
    Task<GovernmentDataCheckResult> CheckSubjectAsync(Subject subject, AccessPurpose purpose, CancellationToken ct);
}
