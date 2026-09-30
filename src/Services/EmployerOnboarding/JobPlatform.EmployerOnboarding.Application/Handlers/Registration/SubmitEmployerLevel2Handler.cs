using JobPlatform.EmployerOnboarding.Application.Commands.Registration;
using JobPlatform.EmployerOnboarding.Application.DTOs.Registration;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Registration;

internal sealed class SubmitEmployerLevel2Handler : ICommandHandler<SubmitEmployerLevel2Command, EmployerRegistrationView>
{
    private readonly IEmployerRegistrationRepository _registrations;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SubmitEmployerLevel2Handler(IEmployerRegistrationRepository registrations, ICurrentUser user, TimeProvider clock)
    {
        _registrations = registrations;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<EmployerRegistrationView>> Handle(SubmitEmployerLevel2Command request, CancellationToken ct)
    {
        var employerAccountId = _user.UserId!.Value;
        var registration = await _registrations.GetByEmployerAsync(employerAccountId, ct);
        if (registration is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "No pending registration was found for this employer account.");
        }

        var identity = new CompanyIdentity(request.CompanyName, request.CompanyId, request.RegistrationNumber);
        var level2 = new Level2Details(request.Website, request.Industry, request.Size, new Address(request.Governorate, request.City, request.Street),
            request.Description);
        registration.SubmitLevel2(identity, level2, ActorFactory.From(_user));
        return ToView(registration);
    }

    internal static EmployerRegistrationView ToView(EmployerRegistration r) => new(
        r.Id, r.EmployerAccountId, r.Status.ToString(),
        r.CompanyIdentity is { } identity ? new CompanyIdentityView(identity.Name, identity.CompanyId, identity.RegistrationNumber) : null,
        r.Level2 is { } level2 ? new Level2View(level2.Website, level2.Industry, level2.Size.ToString(), level2.Address.Governorate, level2.Address.City,
            level2.Address.Street, level2.Description) : null,
        r.OpenedAtUtc, r.ApprovedBy, r.ApprovedAtUtc, r.RowVersion);
}
