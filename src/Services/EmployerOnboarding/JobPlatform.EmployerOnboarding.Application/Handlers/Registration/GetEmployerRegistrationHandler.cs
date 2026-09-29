using JobPlatform.EmployerOnboarding.Application.DTOs.Registration;
using JobPlatform.EmployerOnboarding.Application.Queries.Registration;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Registration;

internal sealed class GetEmployerRegistrationHandler : IQueryHandler<GetEmployerRegistrationQuery, EmployerRegistrationView>
{
    private readonly IEmployerReadStore _store;
    private readonly ICurrentUser _user;

    public GetEmployerRegistrationHandler(IEmployerReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<EmployerRegistrationView>> Handle(GetEmployerRegistrationQuery request, CancellationToken ct) =>
        await _store.GetRegistrationAsync(_user.UserId!.Value, ct) is { } view
            ? view
            : Error.NotFound(Domain.Common.ErrorCodes.NotFound, "No registration was found for this employer account.");
}
