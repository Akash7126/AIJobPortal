using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Application.Interfaces;
using JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.ApiFramework;

internal sealed class GetSoftwareInterfaceDocumentationHandler
    : IQueryHandler<GetSoftwareInterfaceDocumentationQuery, IReadOnlyList<SoftwareInterfaceView>>
{
    private readonly IApiVersionRepository _versions;
    private readonly IExternalIntegrationReadStore _store;

    public GetSoftwareInterfaceDocumentationHandler(IApiVersionRepository versions, IExternalIntegrationReadStore store)
    {
        _versions = versions;
        _store = store;
    }

    public async Task<Result<IReadOnlyList<SoftwareInterfaceView>>> Handle(GetSoftwareInterfaceDocumentationQuery request, CancellationToken ct)
    {
        if (await _versions.GetAsync(request.Version, ct) is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The API version was not found.");
        }

        return Result.Success(await _store.ListSoftwareInterfacesAsync(ct));
    }
}
