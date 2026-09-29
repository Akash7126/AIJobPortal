using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.Profile;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class UpdateLevel1Handler(IProfileRepository profiles, ICurrentUser user, TimeProvider clock) : ICommandHandler<UpdateLevel1Command, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateLevel1Command request, CancellationToken ct)
    {
        var loaded = await ProfileCommandSupport.LoadOwnedAsync(profiles, user, request.IfMatch, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.UpdateLevel1(new Actor(user.UserId!.Value), new FullName(request.FullName), Email.Create(request.Email),
            MobileNumber.Create(request.MobileNumber), Enum.Parse<Gender>(request.Gender, true), user.UserId!.Value, clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
