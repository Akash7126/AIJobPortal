using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

internal sealed class GetApiDocumentationHandler : IQueryHandler<GetApiDocumentationQuery, ApiDocumentationView>
{
    private readonly IApiVersionRepository _versions;

    public GetApiDocumentationHandler(IApiVersionRepository versions) => _versions = versions;

    public async Task<Result<ApiDocumentationView>> Handle(GetApiDocumentationQuery request, CancellationToken ct)
    {
        var version = await _versions.GetAsync(request.Version, ct);
        if (version is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        var content = $"JobPlatform External Integration API {version.Id} — see /openapi/v1.json for the full contract.";
        return new ApiDocumentationView(version.Id, content, version.Status == ApiVersionStatus.Deprecated, version.SunsetAtUtc);
    }
}
