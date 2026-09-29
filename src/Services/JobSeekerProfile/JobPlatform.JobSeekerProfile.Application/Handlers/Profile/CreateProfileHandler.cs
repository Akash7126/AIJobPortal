using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.DTOs.Common;
using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;
using ProfileAggregate = JobPlatform.JobSeekerProfile.Domain.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Handlers.Profile;

internal sealed class CreateProfileHandler(IProfileRepository profiles, IKnownAccountRepository knownAccounts, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CreateProfileCommand, ProfileView>
{
    public async Task<Result<ProfileView>> Handle(CreateProfileCommand request, CancellationToken ct)
    {
        var ownerAccountId = user.UserId!.Value;
        if (await profiles.ExistsByOwnerAsync(ownerAccountId, ct))
        {
            return Error.Conflict("E-JSRPM-DUPLICATE", "A profile already exists for this account.");
        }

        var known = await knownAccounts.GetAsync(ownerAccountId, ct);
        var accountActive = known is { Standing: "Active" };
        var profile = ProfileAggregate.Create(Guid.NewGuid(), ownerAccountId, new FullName(request.FullName), Email.Create(request.Email),
            MobileNumber.Create(request.MobileNumber), Enum.Parse<Gender>(request.Gender, true), accountActive, ownerAccountId, clock.GetUtcNow().UtcDateTime);
        profiles.Add(profile);
        // Built from the in-memory aggregate (not re-queried): the unit of work has not committed yet, so a read-store query would miss it.
        return ToPreliminaryView(profile);
    }

    private static ProfileView ToPreliminaryView(ProfileAggregate p) => new(p.Id, p.OwnerAccountId, p.Status.ToString(), p.FullName.Value, p.Email.Value,
        p.MobileNumber.Value, p.Gender.ToString(), Array.Empty<EducationView>(), Array.Empty<ExperienceView>(), Array.Empty<SkillView>(),
        Array.Empty<TrainingView>(), Array.Empty<CertificateView>(), null, null, null, Array.Empty<SocialLinkView>(), null, null, p.CompletionPercent,
        p.RowVersion);
}
