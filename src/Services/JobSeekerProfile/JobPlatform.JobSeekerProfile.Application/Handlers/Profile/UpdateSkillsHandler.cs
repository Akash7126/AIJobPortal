using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.Profile;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class UpdateSkillsHandler(IProfileRepository profiles, ICurrentUser user, TimeProvider clock) : ICommandHandler<UpdateSkillsCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateSkillsCommand request, CancellationToken ct)
    {
        var loaded = await ProfileCommandSupport.LoadOwnedAsync(profiles, user, request.IfMatch, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.UpdateSkills(new Actor(user.UserId!.Value),
            request.Entries.Select(e => (e.Name, Enum.Parse<SkillKind>(e.Kind, true), Enum.Parse<SkillClass>(e.Class, true))).ToList(), user.UserId!.Value,
            clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
