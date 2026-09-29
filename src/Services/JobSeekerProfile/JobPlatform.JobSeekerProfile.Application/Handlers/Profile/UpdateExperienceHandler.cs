using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.Profile;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class UpdateExperienceHandler(IProfileRepository profiles, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<UpdateExperienceCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateExperienceCommand request, CancellationToken ct)
    {
        var loaded = await ProfileCommandSupport.LoadOwnedAsync(profiles, user, request.IfMatch, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        var actor = new Actor(user.UserId!.Value);
        loaded.Value.UpdateExperience(actor, request.Entries.Select(e => (e.Company, e.Role, e.From, e.To)).ToList(), user.UserId!.Value,
            clock.GetUtcNow().UtcDateTime);
        if (request.YearsOfExperience is not null)
        {
            loaded.Value.UpdateSalaryExpectationAndAddress(actor, loaded.Value.SalaryExpectation, loaded.Value.Address, request.YearsOfExperience,
                user.UserId!.Value, clock.GetUtcNow().UtcDateTime);
        }

        return Result.Success();
    }
}
