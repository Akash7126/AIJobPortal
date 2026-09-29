using JobPlatform.GovernmentIntegration.Application.Commands.Connections;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Connections;

internal sealed class RunSourceReconciliationHandler : ICommandHandler<RunSourceReconciliationCommand, Unit>
{
    private readonly IGovernmentSourceConnectionRepository _connections;
    private readonly IMolRegistryClient _mol;
    private readonly IPefClient _pef;
    private readonly TimeProvider _clock;

    public RunSourceReconciliationHandler(IGovernmentSourceConnectionRepository connections, IMolRegistryClient mol, IPefClient pef, TimeProvider clock)
    {
        _connections = connections;
        _mol = mol;
        _pef = pef;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RunSourceReconciliationCommand request, CancellationToken ct)
    {
        var connection = await _connections.GetBySourceAsync(request.Source, ct);
        if (connection is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "No connection is configured for this source.");
        }

        var result = request.Source switch
        {
            SourceSystem.MoL => await _mol.SyncAsync(ct),
            SourceSystem.PEF => await _pef.SyncAsync(ct),
            _ => new SourceSyncResult(false, null, Domain.Common.ErrorCodes.ConnectionUpstreamTimeout)
        };

        if (result.Success)
        {
            connection.RecordSyncSuccess(result.SnapshotRef!, _clock.GetUtcNow().UtcDateTime);
        }
        else
        {
            connection.RecordSyncFailure();
        }

        return Result.Success();
    }
}
