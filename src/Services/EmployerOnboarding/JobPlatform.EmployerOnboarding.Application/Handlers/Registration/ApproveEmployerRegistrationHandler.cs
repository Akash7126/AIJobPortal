using JobPlatform.EmployerOnboarding.Application.Commands.Registration;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Handlers.Registration;

internal sealed class ApproveEmployerRegistrationHandler : ICommandHandler<ApproveEmployerRegistrationCommand, Unit>
{
    private readonly IEmployerRegistrationRepository _registrations;
    private readonly IEmployerStandingRepository _standings;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ApproveEmployerRegistrationHandler(IEmployerRegistrationRepository registrations, IEmployerStandingRepository standings, ICurrentUser user,
        TimeProvider clock)
    {
        _registrations = registrations;
        _standings = standings;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ApproveEmployerRegistrationCommand request, CancellationToken ct)
    {
        var registration = await _registrations.GetByIdAsync(request.EmployerRegistrationId, ct);
        if (registration is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer registration was not found.");
        }

        registration.Approve(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);

        var standing = await _standings.GetAsync(registration.EmployerAccountId, ct);
        if (standing is null)
        {
            standing = EmployerStanding.OpenFor(Guid.NewGuid(), registration.EmployerAccountId);
            _standings.Add(standing);
        }

        standing.MarkAdmissionApproved(_clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
