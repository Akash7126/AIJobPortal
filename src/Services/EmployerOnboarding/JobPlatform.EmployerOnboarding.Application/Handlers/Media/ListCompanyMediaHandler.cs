using JobPlatform.EmployerOnboarding.Application.DTOs.Media;
using JobPlatform.EmployerOnboarding.Application.Interfaces;
using JobPlatform.EmployerOnboarding.Application.Queries.Media;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Media;

internal sealed class ListCompanyMediaHandler : IQueryHandler<ListCompanyMediaQuery, IReadOnlyList<CompanyMediaView>>
{
    private readonly IEmployerReadStore _store;
    private readonly ICurrentUser _user;

    public ListCompanyMediaHandler(IEmployerReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<CompanyMediaView>>> Handle(ListCompanyMediaQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListMediaAsync(_user.UserId!.Value, ct));
}
