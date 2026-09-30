using JobPlatform.GovernmentIntegration.Application.Commands.Migration;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class RollbackMigrationRunHandler : ICommandHandler<RollbackMigrationRunCommand, Unit>
{
    private readonly IMigrationRunRepository _runs;
    private readonly ILegacyDataRepository _legacyData;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RollbackMigrationRunHandler(IMigrationRunRepository runs, ILegacyDataRepository legacyData, ICurrentUser user, TimeProvider clock)
    {
        _runs = runs;
        _legacyData = legacyData;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RollbackMigrationRunCommand request, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(request.MigrationRunId, ct);
        if (run is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The migration run was not found.");
        }

        run.Rollback(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
