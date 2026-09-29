using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

internal sealed class RetireApiVersionHandler : ICommandHandler<RetireApiVersionCommand, Unit>
{
    private readonly IApiVersionRepository _versions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RetireApiVersionHandler(IApiVersionRepository versions, ICurrentUser user, TimeProvider clock)
    {
        _versions = versions;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RetireApiVersionCommand request, CancellationToken ct)
    {
        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        version.Retire(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
