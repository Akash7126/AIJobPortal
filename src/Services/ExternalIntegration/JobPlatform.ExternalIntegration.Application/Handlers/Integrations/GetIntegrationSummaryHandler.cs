using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;
using JobPlatform.ExternalIntegration.Application.Interfaces;
using JobPlatform.ExternalIntegration.Application.Queries.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class GetIntegrationSummaryHandler : IQueryHandler<GetIntegrationSummaryQuery, IntegrationSummaryView>
{
    private readonly IExternalIntegrationReadStore _store;

    public GetIntegrationSummaryHandler(IExternalIntegrationReadStore store) => _store = store;

    public async Task<Result<IntegrationSummaryView>> Handle(GetIntegrationSummaryQuery request, CancellationToken ct) =>
        await _store.GetIntegrationSummaryAsync(request.SourcePlatformId, ct) is { } view
            ? view
            : Error.NotFound(ErrorCodes.NotFound, "No integration was found for this source platform.");
}
