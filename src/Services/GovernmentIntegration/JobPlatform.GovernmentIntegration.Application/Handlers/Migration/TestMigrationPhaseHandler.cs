using JobPlatform.GovernmentIntegration.Application.Commands.Migration;
using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class TestMigrationPhaseHandler : ICommandHandler<TestMigrationPhaseCommand, MigrationRunView>
{
    private readonly IMigrationRunRepository _runs;
    private readonly TimeProvider _clock;

    public TestMigrationPhaseHandler(IMigrationRunRepository runs, TimeProvider clock)
    {
        _runs = runs;
        _clock = clock;
    }

    public async Task<Result<MigrationRunView>> Handle(TestMigrationPhaseCommand request, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(request.MigrationRunId, ct);
        if (run is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The migration run was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (request.Passed)
        {
            run.AcceptPhase(request.TestOutcome, now);
        }
        else
        {
            run.FailPhase(request.TestOutcome, now);
        }

        return StartDataMigrationHandler.ToView(run);
    }
}
