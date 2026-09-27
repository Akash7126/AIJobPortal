using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence;

internal sealed class GovernmentIntegrationReadStore(GovernmentIntegrationDbContext db) : IGovernmentIntegrationReadStore
{
    private static readonly VerificationState[] PendingReviewStates = { VerificationState.PendingManualReview };

    public async Task<EmployerVerificationView?> GetEmployerVerificationAsync(Guid id, CancellationToken ct = default)
    {
        var v = await db.EmployerVerifications.AsNoTracking().Include(x => x.Attempts).FirstOrDefaultAsync(x => x.Id == id, ct);
        return v is null ? null : ToView(v);
    }

    public async Task<EmployerVerificationView?> GetActiveEmployerVerificationForEmployerAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        var v = await db.EmployerVerifications.AsNoTracking().Include(x => x.Attempts)
            .Where(x => x.EmployerAccountId == employerAccountId)
            .OrderByDescending(x => x.DecidedAtUtc)
            .FirstOrDefaultAsync(ct);
        return v is null ? null : ToView(v);
    }

    public async Task<PagedResult<EmployerVerificationView>> ListPendingManualReviewAsync(PageRequest page, CancellationToken ct = default)
    {
        var query = db.EmployerVerifications.AsNoTracking().Include(x => x.Attempts).Where(x => PendingReviewStates.Contains(x.State));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<EmployerVerificationView>(items.Select(ToView).ToArray(), page.Page, page.PageSize, total);
    }

    public async Task<SubjectVerificationStatusView> GetSubjectVerificationStatusAsync(SubjectType subjectType, Guid subjectId, CancellationToken ct = default)
    {
        var govData = await db.GovernmentVerificationData.AsNoTracking()
            .Where(d => d.Subject.SubjectType == subjectType && d.Subject.SubjectId == subjectId)
            .OrderByDescending(d => d.ImportedAtUtc).Select(d => (string?)d.Status.ToString()).FirstOrDefaultAsync(ct);
        var educational = await db.EducationalCredentialVerifications.AsNoTracking().Where(e => e.SubjectId == subjectId)
            .OrderByDescending(e => e.Id).Select(e => (string?)e.Status.ToString()).FirstOrDefaultAsync(ct);
        var identity = await db.IdentityVerifications.AsNoTracking().Where(i => i.Subject.SubjectId == subjectId)
            .OrderByDescending(i => i.Id).Select(i => (string?)i.Status.ToString()).FirstOrDefaultAsync(ct);
        return new SubjectVerificationStatusView(subjectType.ToString(), subjectId, govData, educational, identity);
    }

    public async Task<IReadOnlyList<GovernmentSourceConnectionView>> ListGovernmentSourceConnectionsAsync(CancellationToken ct = default) =>
        await db.GovernmentSourceConnections.AsNoTracking().OrderBy(c => c.Source)
            .Select(c => new GovernmentSourceConnectionView(c.Id, c.Source.ToString(), c.Endpoint, c.AuthMethod, c.Enabled, c.Health.ToString(),
                c.LastSuccessfulSyncAtUtc))
            .ToListAsync(ct);

    public async Task<MigrationRunView?> GetMigrationRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await db.MigrationRuns.AsNoTracking().Include(r => r.Phases).Include(r => r.Log).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
        {
            return null;
        }

        return new MigrationRunView(run.Id, run.InitiatedBy, run.Status.ToString(),
            run.Phases.Select(p => new MigrationPhaseView(p.Name, p.Status.ToString(), p.TestOutcome)).ToArray(),
            run.Log.Select(l => new MigrationLogEntryView(l.Phase, l.Outcome, l.Message, l.AtUtc)).ToArray());
    }

    public async Task<DataQualityView?> GetDataQualityByBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var d = await db.DataQuality.AsNoTracking().Where(x => x.BatchId == batchId).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return d is null
            ? null
            : new DataQualityView(d.Id, d.MigrationRunId, d.BatchId, d.Status.ToString(), d.IssuesResolved, d.DuplicatesRemoved, d.FormatsStandardized,
                d.RecordsChecked, d.RecordsRejected);
    }

    public async Task<PagedResult<GovernmentAccessLogView>> ListGovernmentExchangesAsync(DateTime? from, DateTime? to, PageRequest page,
        CancellationToken ct = default)
    {
        var query = db.GovernmentDataAccessLog.AsNoTracking().AsQueryable();
        if (from is { } f)
        {
            query = query.Where(l => l.OccurredAtUtc >= f);
        }

        if (to is { } t)
        {
            query = query.Where(l => l.OccurredAtUtc <= t);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.OccurredAtUtc).Skip(page.Skip).Take(page.PageSize)
            .Select(l => new GovernmentAccessLogView(l.Id, l.OccurredAtUtc, l.Component, l.Purpose.ToString(), l.SubjectRef, l.Decision, l.ErrorCode))
            .ToListAsync(ct);
        return new PagedResult<GovernmentAccessLogView>(items, page.Page, page.PageSize, total);
    }

    private static EmployerVerificationView ToView(EmployerVerification v) => new(
        v.Id, v.EmployerAccountId, v.State.ToString(), v.Method.ToString(), v.AttemptCount, v.DecidedBy, v.DecidedAtUtc, v.FailureReason,
        v.Attempts.Select(a => new VerificationAttemptView(a.AttemptNo, a.Source.ToString(), a.Outcome.ToString(), a.ErrorCode, a.StartedAtUtc)).ToArray(),
        v.RowVersion);
}
