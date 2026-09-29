using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Application.Queries.EmployerVerifications;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.EmployerVerifications;

internal sealed class ListPendingManualReviewHandler : IQueryHandler<ListPendingManualReviewQuery, PagedResult<EmployerVerificationView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public ListPendingManualReviewHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<PagedResult<EmployerVerificationView>>> Handle(ListPendingManualReviewQuery request, CancellationToken ct) =>
        await _store.ListPendingManualReviewAsync(new PageRequest(request.Page, request.PageSize), ct);
}
