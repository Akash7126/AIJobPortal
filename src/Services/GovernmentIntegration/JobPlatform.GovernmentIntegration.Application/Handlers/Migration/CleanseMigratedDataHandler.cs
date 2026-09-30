using JobPlatform.GovernmentIntegration.Application.Commands.Migration;
using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class CleanseMigratedDataHandler : ICommandHandler<CleanseMigratedDataCommand, DataQualityView>
{
    private readonly IDataQualityRepository _repository;
    private readonly TimeProvider _clock;

    public CleanseMigratedDataHandler(IDataQualityRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>INV-13 snapshot isolation: a batch already running a cleansing pass refuses a second concurrent one (AC-03; the handover's
    /// "queued, processed after Complete" is realised here as an immediate 409 - the caller retries once the running pass completes).</summary>
    public async Task<Result<DataQualityView>> Handle(CleanseMigratedDataCommand request, CancellationToken ct)
    {
        if (await _repository.HasRunningForBatchAsync(request.BatchId, ct))
        {
            return Error.Conflict(Domain.Common.ErrorCodes.DataQualitySnapshotIsolation, "A cleansing run is already active for this batch.");
        }

        var dataQuality = DataQuality.Start(Guid.NewGuid(), request.MigrationRunId, request.BatchId, request.SnapshotVersion);
        _repository.Add(dataQuality);
        dataQuality.Record(request.IssuesResolved, request.DuplicatesRemoved, request.FormatsStandardized, request.RecordsChecked, request.RecordsRejected);
        dataQuality.Complete(_clock.GetUtcNow().UtcDateTime);
        return ToView(dataQuality);
    }

    internal static DataQualityView ToView(DataQuality d) => new(d.Id, d.MigrationRunId, d.BatchId, d.Status.ToString(), d.IssuesResolved,
        d.DuplicatesRemoved, d.FormatsStandardized, d.RecordsChecked, d.RecordsRejected);
}
