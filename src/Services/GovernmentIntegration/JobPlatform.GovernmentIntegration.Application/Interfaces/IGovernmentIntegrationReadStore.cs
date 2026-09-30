using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;
using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Application.DTOs.Verifications;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.GovernmentIntegration.Application.Interfaces;

/// <summary>Read side (handover section 8.3): dedicated projections, joined and paged in SQL - never via the aggregates.</summary>
public interface IGovernmentIntegrationReadStore
{
    Task<EmployerVerificationView?> GetEmployerVerificationAsync(Guid id, CancellationToken ct = default);

    Task<EmployerVerificationView?> GetActiveEmployerVerificationForEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<PagedResult<EmployerVerificationView>> ListPendingManualReviewAsync(PageRequest page, CancellationToken ct = default);

    Task<SubjectVerificationStatusView> GetSubjectVerificationStatusAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default);

    Task<IReadOnlyList<GovernmentSourceConnectionView>> ListGovernmentSourceConnectionsAsync(CancellationToken ct = default);

    Task<MigrationRunView?> GetMigrationRunAsync(Guid id, CancellationToken ct = default);

    Task<DataQualityView?> GetDataQualityByBatchAsync(Guid batchId, CancellationToken ct = default);

    Task<PagedResult<GovernmentAccessLogView>> ListGovernmentExchangesAsync(DateTime? from, DateTime? to, PageRequest page, CancellationToken ct = default);
}
