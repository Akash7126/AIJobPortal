using JobPlatform.GovernmentIntegration.Application.Commands.Migration;
using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class StartDataMigrationHandler : ICommandHandler<StartDataMigrationCommand, MigrationRunView>
{
    private readonly IMigrationRunRepository _runs;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public StartDataMigrationHandler(IMigrationRunRepository runs, ICurrentUser user, TimeProvider clock)
    {
        _runs = runs;
        _user = user;
        _clock = clock;
    }

    /// <summary>Single-run guard (handover section 3.7): a Redis lock protects concurrent starts in production; here the repository check
    /// against GetActiveAsync is the same guard expressed at the application level, sufficient for the SQLite/in-memory test topology.</summary>
    public async Task<Result<MigrationRunView>> Handle(StartDataMigrationCommand request, CancellationToken ct)
    {
        if (await _runs.GetActiveAsync(ct) is not null)
        {
            return Error.Conflict(Domain.Common.ErrorCodes.MigrationAlreadyActive, "A migration run is already active.");
        }

        var run = MigrationRun.Start(Guid.NewGuid(), ActorFactory.From(_user), request.Phases, _clock.GetUtcNow().UtcDateTime);
        _runs.Add(run);
        return ToView(run);
    }

    internal static MigrationRunView ToView(MigrationRun r) => new(r.Id, r.InitiatedBy, r.Status.ToString(),
        r.Phases.Select(p => new MigrationPhaseView(p.Name, p.Status.ToString(), p.TestOutcome)).ToArray(),
        r.Log.Select(l => new MigrationLogEntryView(l.Phase, l.Outcome, l.Message, l.AtUtc)).ToArray());
}
