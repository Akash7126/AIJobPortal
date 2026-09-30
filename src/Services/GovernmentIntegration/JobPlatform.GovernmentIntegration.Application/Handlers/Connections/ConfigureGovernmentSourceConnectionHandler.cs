using JobPlatform.GovernmentIntegration.Application.Commands.Connections;
using JobPlatform.GovernmentIntegration.Application.DTOs.Connections;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Connections;

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
