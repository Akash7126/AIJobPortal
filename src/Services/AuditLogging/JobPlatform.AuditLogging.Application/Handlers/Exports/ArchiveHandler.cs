using FluentValidation;
using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.Exports;

internal sealed class ArchiveHandler : ICommandHandler<ArchiveExpiredAuditEntriesCommand, int>
{
    private readonly IAuditEntryRepository _entries;
    private readonly RetentionPolicy _retention;
    private readonly TimeProvider _clock;

    public ArchiveHandler(IAuditEntryRepository entries, RetentionPolicy retention, TimeProvider clock)
    {
        _entries = entries;
        _retention = retention;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(ArchiveExpiredAuditEntriesCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expired = await _entries.ListExpiredAsync(now, request.BatchSize, ct);
        var archived = 0;
        foreach (var entry in expired.Where(e => _retention.IsExpired(e.RetainUntilUtc, now)))
        {
            entry.Archive(now);
            archived++;
        }

        return archived;
    }
}
