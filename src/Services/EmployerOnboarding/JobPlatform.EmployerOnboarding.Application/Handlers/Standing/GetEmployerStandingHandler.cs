using JobPlatform.EmployerOnboarding.Application.DTOs.Standing;
using JobPlatform.EmployerOnboarding.Application.Queries.Standing;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Standing;

internal sealed class GetEmployerStandingHandler : IQueryHandler<GetEmployerStandingQuery, EmployerStandingView>
{
    private readonly IEmployerCache _cache;
    private readonly IEmployerReadStore _store;

    public GetEmployerStandingHandler(IEmployerCache cache, IEmployerReadStore store)
    {
        _cache = cache;
        _store = store;
    }

    public async Task<Result<EmployerStandingView>> Handle(GetEmployerStandingQuery request, CancellationToken ct)
    {
        if (await _cache.GetStandingAsync(request.EmployerAccountId, ct) is { } cached)
        {
            return cached;
        }

        var view = await _store.GetStandingAsync(request.EmployerAccountId, ct);
        if (view is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer was not found.");
        }

        await _cache.SetStandingAsync(request.EmployerAccountId, view, ct);
        return view;
    }
}
