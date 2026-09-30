using JobPlatform.GovernmentIntegration.Application.Commands.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.EmployerVerifications;

internal sealed class DecideEmployerVerificationManuallyHandler : ICommandHandler<DecideEmployerVerificationManuallyCommand, Unit>
{
    private readonly IEmployerVerificationRepository _verifications;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public DecideEmployerVerificationManuallyHandler(IEmployerVerificationRepository verifications, ICurrentUser user, TimeProvider clock)
    {
        _verifications = verifications;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(DecideEmployerVerificationManuallyCommand request, CancellationToken ct)
    {
        var verification = await _verifications.GetByIdAsync(request.EmployerVerificationId, ct);
        if (verification is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer verification was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var reviewerId = _user.UserId!.Value;
        if (request.Decision == ManualDecision.Approve)
        {
            verification.ApproveManually(reviewerId, now);
        }
        else
        {
            verification.RejectManually(reviewerId, request.Reason!, now);
        }

        return Result.Success();
    }
}
