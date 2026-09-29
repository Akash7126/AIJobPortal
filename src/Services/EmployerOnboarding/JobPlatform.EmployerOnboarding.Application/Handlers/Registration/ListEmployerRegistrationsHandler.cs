using JobPlatform.EmployerOnboarding.Application.DTOs.Registration;
using JobPlatform.EmployerOnboarding.Application.Queries.Registration;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Registration;

internal sealed class ListEmployerRegistrationsHandler : IQueryHandler<ListEmployerRegistrationsQuery, PagedResult<EmployerRegistrationView>>
{
    private readonly IEmployerReadStore _store;

    public ListEmployerRegistrationsHandler(IEmployerReadStore store) => _store = store;

    public async Task<Result<PagedResult<EmployerRegistrationView>>> Handle(ListEmployerRegistrationsQuery request, CancellationToken ct) =>
        await _store.ListRegistrationsAsync(request.Status, new PageRequest(request.Page, request.PageSize), ct);
}
