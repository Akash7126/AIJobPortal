using JobPlatform.Reporting.Application.Ingestion;
using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.Reporting.Application.Services.Ingestion;

/// <summary>
/// Generic ingestion (K-3): dedupe by MessageId, append the FactEvent, update the daily rollups. Returns false for a redelivery so the projectors are not run twice.
/// Actor ids are pseudonymised here; nothing personal is stored.
/// </summary>
public sealed class EventIngestionService
{
    private readonly IFactStore _facts;
    private readonly IOptions<ReportingOptions> _options;
    private readonly ILogger<EventIngestionService> _logger;

    public EventIngestionService(IFactStore facts, IOptions<ReportingOptions> options, ILogger<EventIngestionService> logger)
    {
        _facts = facts;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> RecordAsync(IIntegrationEvent e, CancellationToken ct)
    {
        var description = EventCatalog.Describe(e) ?? throw new InvalidOperationException($"Event {e.EventType} is not in the reporting event catalogue.");
        if (await _facts.EventExistsAsync(e.MessageId, ct))
        {
            _logger.LogInformation("Duplicate delivery of {EventType} {MessageId} ignored", e.EventType, e.MessageId);
            return false;
        }

        _facts.Add(FactEvent.Record(e.MessageId, e.Producer, e.EventType, description.ActivityType, e.OccurredOnUtc, description.ActorType,
            ActorKeys.Pseudonymise(description.ActorId, _options.Value.ActorKeySalt), description.SubjectId));
        var day = DateOnly.FromDateTime(e.OccurredOnUtc);
        await _facts.AddToDailyAsync(day, "event." + e.EventType, 1, ct);
        await _facts.AddToDailyAsync(day, "activity." + description.ActivityType, 1, ct);
        return true;
    }
}
