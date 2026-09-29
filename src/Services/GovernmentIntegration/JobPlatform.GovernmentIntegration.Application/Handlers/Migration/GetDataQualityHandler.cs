using JobPlatform.GovernmentIntegration.Application.DTOs.Migration;
using JobPlatform.GovernmentIntegration.Application.Queries.Migration;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Migration;

internal sealed class GetDataQualityHandler : IQueryHandler<GetDataQualityQuery, DataQualityView>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetDataQualityHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<DataQualityView>> Handle(GetDataQualityQuery request, CancellationToken ct) =>
        await _store.GetDataQualityByBatchAsync(request.BatchId, ct) is { } view
            ? view
            : Error.NotFound(Domain.Common.ErrorCodes.NotFound, "No data-quality run was found for this batch.");
}
