using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.Profile;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class UpdateEducationHandler(IProfileRepository profiles, ICurrentUser user, TimeProvider clock) : ICommandHandler<UpdateEducationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateEducationCommand request, CancellationToken ct)
    {
        var loaded = await ProfileCommandSupport.LoadOwnedAsync(profiles, user, request.IfMatch, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.UpdateEducation(new Actor(user.UserId!.Value), request.Entries.Select(e => (e.Degree, e.Institution, e.From, e.To)).ToList(),
            user.UserId!.Value, clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
