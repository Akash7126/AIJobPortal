using JobPlatform.GovernmentIntegration.Application.Commands.Migration;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class ImportLegacyDataBatchHandler : ICommandHandler<ImportLegacyDataBatchCommand, int>
{
    private readonly ILegacyDataRepository _repository;
    private readonly ILegacySourceReader _reader;
    private readonly TimeProvider _clock;

    public ImportLegacyDataBatchHandler(ILegacyDataRepository repository, ILegacySourceReader reader, TimeProvider clock)
    {
        _repository = repository;
        _reader = reader;
        _clock = clock;
    }

    /// <summary>INV-11 idempotency: a record already known for (SourceSystem, SourceRecordId) is left unchanged (AC-04). Mapping/transformation
    /// are identity stubs (handover Q-04: the real MoL/PEF schema is unknown) so the pipeline's stage machine and validation are exercised
    /// end-to-end without depending on an unavailable schema.</summary>
    public async Task<Result<int>> Handle(ImportLegacyDataBatchCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var imported = 0;
        foreach (var record in await _reader.ReadBatchAsync(request.SourceSystem, request.Take, ct))
        {
            if (await _repository.GetBySourceKeyAsync(request.SourceSystem, record.SourceRecordId, ct) is not null)
            {
                continue;
            }

            var legacy = LegacyData.Import(Guid.NewGuid(), request.MigrationRunId, request.MigrationBatchId, request.SourceSystem,
                record.SourceRecordId, record.RecordType, record.Payload);
            legacy.MapToNewSchema(record.Payload);
            legacy.Transform(record.Payload);
            var errors = string.IsNullOrWhiteSpace(record.Payload) ? new[] { Domain.Common.ErrorCodes.LegacyInvalidField } : Array.Empty<string>();
            legacy.Validate(errors);
            if (legacy.Stage == LegacyStage.Validated)
            {
                legacy.MarkMigrated(now);
                imported++;
            }

            _repository.Add(legacy);
        }

        return imported;
    }
}
