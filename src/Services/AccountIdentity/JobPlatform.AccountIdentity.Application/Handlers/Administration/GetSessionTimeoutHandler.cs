using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Queries.Administration;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Administration;

internal sealed class GetSessionTimeoutHandler : IQueryHandler<GetSessionTimeoutQuery, SessionTimeoutView>
{
    private readonly IIdentityReadStore _store;

    public GetSessionTimeoutHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<SessionTimeoutView>> Handle(GetSessionTimeoutQuery request, CancellationToken ct) =>
        await _store.GetSessionTimeoutAsync(ct);
}
