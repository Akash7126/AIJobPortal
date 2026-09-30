using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum MigrationStatus
{
    Created,
    Running,
    PhaseFailed,
    RolledBack,
    Completed
}

public enum MigrationPhaseStatus
{
    Pending,
    Running,
    Accepted,
    Failed
}

public sealed class MigrationPhase : Entity<Guid>
{
    private MigrationPhase()
    {
    }

    internal MigrationPhase(Guid id, string name)
    {
        Id = id;
        Name = name;
        Status = MigrationPhaseStatus.Pending;
    }

    public string Name { get; private set; } = string.Empty;
    public MigrationPhaseStatus Status { get; private set; }
    public string? TestOutcome { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }

    internal void Begin(DateTime nowUtc)
    {
        Status = MigrationPhaseStatus.Running;
        StartedAtUtc = nowUtc;
    }

    internal void Accept(string testOutcome, DateTime nowUtc)
    {
        Status = MigrationPhaseStatus.Accepted;
        TestOutcome = testOutcome;
        EndedAtUtc = nowUtc;
    }

    internal void Fail(string testOutcome, DateTime nowUtc)
    {
        Status = MigrationPhaseStatus.Failed;
        TestOutcome = testOutcome;
        EndedAtUtc = nowUtc;
    }
}

/// <summary>AC-04 of US-6.1-01/6.1-03: every outcome of the migration run writes a log entry.</summary>
public sealed class MigrationLogEntry : Entity<Guid>
{
    private MigrationLogEntry()
    {
    }

    internal MigrationLogEntry(Guid id, string phase, string outcome, string message, DateTime atUtc)
    {
        Id = id;
        Phase = phase;
        Outcome = outcome;
        Message = message;
        AtUtc = atUtc;
    }

    public string Phase { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTime AtUtc { get; private set; }
}

/// <summary>
/// Proposed (handover section 3.7): orchestration state for the one saga in the system (LegacyDataImportSaga), realising US-6.1-01 and
/// US-6.1-03. No integration event is catalogued for it - LegacyDataImported and DataQualityUpdated (published by the aggregates the saga
/// drives) are the externally visible signal of its progress.
/// </summary>
public sealed class MigrationRun : AggregateRoot<Guid>
{
    private readonly List<MigrationPhase> _phases = new();
    private readonly List<MigrationLogEntry> _log = new();

    private MigrationRun()
    {
    }

    public Guid InitiatedBy { get; private set; }
    public MigrationStatus Status { get; private set; }
    public int CurrentPhaseIndex { get; private set; }
    public IReadOnlyList<MigrationPhase> Phases => _phases;
    public IReadOnlyList<MigrationLogEntry> Log => _log;

    public MigrationPhase? CurrentPhase => CurrentPhaseIndex >= 0 && CurrentPhaseIndex < _phases.Count ? _phases[CurrentPhaseIndex] : null;

    /// <summary>INV-14: only administrators may start a migration run.</summary>
    public static MigrationRun Start(Guid id, Actor actor, IReadOnlyList<string> phaseNames, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.MigrationAdminOnly));
        Check(new BusinessRule("GI.Migration.NO_PHASES", "At least one phase is required.", phaseNames.Count == 0, ErrorCodes.Forbidden,
            BusinessRuleKind.BusinessRule));

        var run = new MigrationRun { Id = id, InitiatedBy = actor.Id, Status = MigrationStatus.Running, CurrentPhaseIndex = 0 };
        foreach (var name in phaseNames)
        {
            run._phases.Add(new MigrationPhase(Guid.NewGuid(), name));
        }

        run._phases[0].Begin(nowUtc);
        run._log.Add(new MigrationLogEntry(Guid.NewGuid(), run._phases[0].Name, "Started", "Migration run started.", nowUtc));
        return run;
    }

    /// <summary>AC-01 of US-6.1-03: the phase test passed, so the run advances to the next phase (or completes on the last one).</summary>
    public void AcceptPhase(string testOutcome, DateTime nowUtc)
    {
        Check(RunningRule());
        var phase = CurrentPhase ?? throw new InvalidOperationException("There is no current phase.");
        phase.Accept(testOutcome, nowUtc);
        _log.Add(new MigrationLogEntry(Guid.NewGuid(), phase.Name, "Accepted", testOutcome, nowUtc));

        if (CurrentPhaseIndex == _phases.Count - 1)
        {
            Status = MigrationStatus.Completed;
            return;
        }

        CurrentPhaseIndex++;
        _phases[CurrentPhaseIndex].Begin(nowUtc);
    }

    /// <summary>AC-02: a failed phase test compensates (rollback) and does not proceed to the next phase.</summary>
    public void FailPhase(string reason, DateTime nowUtc)
    {
        Check(RunningRule());
        var phase = CurrentPhase ?? throw new InvalidOperationException("There is no current phase.");
        phase.Fail(reason, nowUtc);
        Status = MigrationStatus.PhaseFailed;
        _log.Add(new MigrationLogEntry(Guid.NewGuid(), phase.Name, "Failed", reason, nowUtc));
    }

    /// <summary>INV-15: only administrators may roll back. AC-03: restores prior state (the staging batch is voided by the application).</summary>
    public void Rollback(Actor actor, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.MigrationAdminOnly));
        Check(new BusinessRule(RuleCodes.MigrationNoActiveRun, "Only a failed or running migration run can be rolled back.",
            Status is not (MigrationStatus.Running or MigrationStatus.PhaseFailed), ErrorCodes.MigrationNoActiveRun, BusinessRuleKind.Conflict));
        Status = MigrationStatus.RolledBack;
        _log.Add(new MigrationLogEntry(Guid.NewGuid(), CurrentPhase?.Name ?? "-", "RolledBack", "Migration run rolled back.", nowUtc));
    }

    private IBusinessRule RunningRule() =>
        new BusinessRule("GI.Migration.NOT_RUNNING", "This action requires the migration run to be running.", Status != MigrationStatus.Running,
            ErrorCodes.MigrationNoActiveRun, BusinessRuleKind.Conflict);
}
