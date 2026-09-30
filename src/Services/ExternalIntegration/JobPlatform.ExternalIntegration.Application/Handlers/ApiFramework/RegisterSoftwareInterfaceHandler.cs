using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

internal sealed class RegisterSoftwareInterfaceHandler : ICommandHandler<RegisterSoftwareInterfaceCommand, SoftwareInterfaceView>
{
    private readonly ISoftwareInterfaceRepository _interfaces;
    private readonly ICurrentUser _user;

    public RegisterSoftwareInterfaceHandler(ISoftwareInterfaceRepository interfaces, ICurrentUser user)
    {
        _interfaces = interfaces;
        _user = user;
    }

    public async Task<Result<SoftwareInterfaceView>> Handle(RegisterSoftwareInterfaceCommand request, CancellationToken ct)
    {
        var category = Enum.Parse<SoftwareInterfaceCategory>(request.Category, true);
        var actor = ActorFactory.From(_user);
        var existing = await _interfaces.GetByKeyAsync(category, request.Name, ct);
        if (existing is not null)
        {
            // AC-05: idempotent — a repeat registration updates the endpoint rather than creating a duplicate.
            existing.UpdateEndpoint(request.Endpoint, actor);
            return ToView(existing);
        }

        var connection = SoftwareInterfaceConnection.Register(Guid.NewGuid(), category, request.Name, request.Endpoint, actor);
        _interfaces.Add(connection);
        return ToView(connection);
    }

    private static SoftwareInterfaceView ToView(SoftwareInterfaceConnection c) => new(c.Id, c.Category.ToString(), c.Name, c.Endpoint, c.Enabled);
}
