using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

internal sealed class ConfigureApiDataFormatHandler : ICommandHandler<ConfigureApiDataFormatCommand, Unit>
{
    private readonly IApiVersionRepository _versions;
    private readonly ICurrentUser _user;

    public ConfigureApiDataFormatHandler(IApiVersionRepository versions, ICurrentUser user)
    {
        _versions = versions;
        _user = user;
    }

    public async Task<Result<Unit>> Handle(ConfigureApiDataFormatCommand request, CancellationToken ct)
    {
        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        version.ConfigureAcceptedFormats(request.Formats, ActorFactory.From(_user));
        return Result.Success();
    }
}
