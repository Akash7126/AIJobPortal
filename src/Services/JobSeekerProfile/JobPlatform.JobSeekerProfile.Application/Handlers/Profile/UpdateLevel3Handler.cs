using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.Profile;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class UpdateLevel3Handler(IProfileRepository profiles, ICurrentUser user, TimeProvider clock) : ICommandHandler<UpdateLevel3Command, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateLevel3Command request, CancellationToken ct)
    {
        var loaded = await ProfileCommandSupport.LoadOwnedAsync(profiles, user, request.IfMatch, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.UpdateLevel3(new Actor(user.UserId!.Value), request.SocialLinks.Select(l => (l.Network, l.Url)).ToList(), request.Statement,
            request.Bio, user.UserId!.Value, clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
