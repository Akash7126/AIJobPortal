using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;
using JobPlatform.ExternalIntegration.Application.Queries.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.ExternalIntegration.Application.Handlers.Integrations;

internal sealed class GetIntegrationHandler : IQueryHandler<GetIntegrationQuery, IntegrationView>
{
    private readonly IExternalIntegrationReadStore _store;
    private readonly ICurrentUser _user;

    public GetIntegrationHandler(IExternalIntegrationReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<IntegrationView>> Handle(GetIntegrationQuery request, CancellationToken ct) =>
        await _store.GetIntegrationByPartnerAsync(_user.UserId!.Value, ct) is { } view
            ? view
            : Error.NotFound(ErrorCodes.NotFound, "No integration was found for this partner account.");
}
