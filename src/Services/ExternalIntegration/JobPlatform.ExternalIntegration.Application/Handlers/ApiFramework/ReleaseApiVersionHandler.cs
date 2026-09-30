using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

internal sealed class ReleaseApiVersionHandler : ICommandHandler<ReleaseApiVersionCommand, ApiVersionView>
{
    private readonly IApiVersionRepository _versions;

    public ReleaseApiVersionHandler(IApiVersionRepository versions) => _versions = versions;

    public async Task<Result<ApiVersionView>> Handle(ReleaseApiVersionCommand request, CancellationToken ct)
    {
        if (await _versions.GetAsync(request.Version, ct) is not null)
        {
            return Error.Conflict(ErrorCodes.ApiInvalidTransition, "This API version has already been released.");
        }

        var version = Domain.ApiVersion.Release(request.Version);
        _versions.Add(version);
        return ToView(version);
    }

    internal static ApiVersionView ToView(Domain.ApiVersion v) => new(v.Id, v.Status.ToString(), v.DeprecatedAtUtc, v.SunsetAtUtc, v.AcceptedFormats);
}
