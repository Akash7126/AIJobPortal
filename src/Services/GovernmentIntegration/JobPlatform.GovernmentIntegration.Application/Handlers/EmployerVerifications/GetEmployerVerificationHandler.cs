using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Application.Queries.EmployerVerifications;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.EmployerVerifications;

internal sealed class GetEmployerVerificationHandler : IQueryHandler<GetEmployerVerificationQuery, EmployerVerificationView>
{
    private readonly IGovernmentIntegrationReadStore _store;
    private readonly ICurrentUser _user;

    public GetEmployerVerificationHandler(IGovernmentIntegrationReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<EmployerVerificationView>> Handle(GetEmployerVerificationQuery request, CancellationToken ct)
    {
        var view = await _store.GetEmployerVerificationAsync(request.EmployerVerificationId, ct);
        if (view is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer verification was not found.");
        }

        if (_user.ActorType == ActorType.Employer && view.EmployerAccountId != _user.UserId)
        {
            return Error.Forbidden(Domain.Common.ErrorCodes.Forbidden, "You may only view your own employer verification.");
        }

        return view;
    }
}
