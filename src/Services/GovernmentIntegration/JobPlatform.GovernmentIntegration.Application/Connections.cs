using FluentValidation;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application;

// ============================================================ US-3.4.2-02 / US-2.5-01 GovernmentSourceConnection

public sealed record ConfigureGovernmentSourceConnectionCommand(SourceSystem Source, string Endpoint, string AuthMethod, string CredentialRef, bool Enabled)
    : AdminCommand<GovernmentSourceConnectionView>;

public sealed class ConfigureGovernmentSourceConnectionValidator : AbstractValidator<ConfigureGovernmentSourceConnectionCommand>
{
    public ConfigureGovernmentSourceConnectionValidator()
    {
        RuleFor(c => c.Source).IsInEnum().WithErrorCode("VAL.Source.Invalid");
        RuleFor(c => c.Endpoint).NotEmpty().Must(BeAnAbsoluteHttpsUrl).WithErrorCode("VAL.Endpoint.MustBeHttps");
        RuleFor(c => c.CredentialRef).NotEmpty().WithErrorCode("VAL.CredentialRef.Required");
    }

    private static bool BeAnAbsoluteHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

/// <summary>US-2.5-04 AC-04: re-applying the same configuration is idempotent (upsert keyed by Source, no duplicate side effects).</summary>
internal sealed class ConfigureGovernmentSourceConnectionHandler : ICommandHandler<ConfigureGovernmentSourceConnectionCommand, GovernmentSourceConnectionView>
{
    private readonly IGovernmentSourceConnectionRepository _connections;
    private readonly ICurrentUser _user;

    public ConfigureGovernmentSourceConnectionHandler(IGovernmentSourceConnectionRepository connections, ICurrentUser user)
    {
        _connections = connections;
        _user = user;
    }

    public async Task<Result<GovernmentSourceConnectionView>> Handle(ConfigureGovernmentSourceConnectionCommand request, CancellationToken ct)
    {
        var actor = ActorFactory.From(_user);
        var connection = await _connections.GetBySourceAsync(request.Source, ct);
        if (connection is null)
        {
            connection = GovernmentSourceConnection.Configure(Guid.NewGuid(), actor, request.Source, request.Endpoint, request.AuthMethod,
                request.CredentialRef, request.Enabled);
            _connections.Add(connection);
        }
        else
        {
            connection.Update(actor, request.Endpoint, request.AuthMethod, request.CredentialRef, request.Enabled);
        }

        return ToView(connection);
    }

    internal static GovernmentSourceConnectionView ToView(GovernmentSourceConnection c) =>
        new(c.Id, c.Source.ToString(), c.Endpoint, c.AuthMethod, c.Enabled, c.Health.ToString(), c.LastSuccessfulSyncAtUtc);
}

public sealed record ListGovernmentSourceConnectionsQuery : AdminQuery<IReadOnlyList<GovernmentSourceConnectionView>>;

internal sealed class ListGovernmentSourceConnectionsHandler : IQueryHandler<ListGovernmentSourceConnectionsQuery, IReadOnlyList<GovernmentSourceConnectionView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public ListGovernmentSourceConnectionsHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<GovernmentSourceConnectionView>>> Handle(ListGovernmentSourceConnectionsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListGovernmentSourceConnectionsAsync(ct));
}

/// <summary>F-0001 (US-4.3-01): BC-02 reads the connection catalogue and health synchronously rather than via an event (handover Q-06:
/// left open in the source's favour - no ConnectionHealthChanged event is published).</summary>
public sealed record GetGovernmentSystemsQuery : ServiceQuery<IReadOnlyList<GovernmentSourceConnectionView>>;

internal sealed class GetGovernmentSystemsHandler : IQueryHandler<GetGovernmentSystemsQuery, IReadOnlyList<GovernmentSourceConnectionView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetGovernmentSystemsHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<IReadOnlyList<GovernmentSourceConnectionView>>> Handle(GetGovernmentSystemsQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListGovernmentSourceConnectionsAsync(ct));
}

// ============================================================ US-2.5-01 scheduled/triggered reconciliation

public sealed record RunSourceReconciliationCommand(SourceSystem Source) : ServiceCommand<Unit>;

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
