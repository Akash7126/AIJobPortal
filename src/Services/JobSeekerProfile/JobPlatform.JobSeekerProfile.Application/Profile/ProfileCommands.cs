using FluentValidation;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.ValueObjects;
using ProfileAggregate = JobPlatform.JobSeekerProfile.Domain.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Profile;

public sealed record EducationInput(string Degree, string Institution, DateTime? From, DateTime? To);
public sealed record ExperienceInput(string Company, string Role, DateTime? From, DateTime? To);
public sealed record SkillInput(string Name, string Kind, string Class);
public sealed record TrainingInput(string Name, string? Provider, DateTime? CompletedOn);
public sealed record CertificateInput(string Name, string? Issuer, DateTime? IssuedOn);
public sealed record SocialLinkInput(string Network, string Url);

public sealed record CreateProfileCommand(string FullName, string Email, string MobileNumber, string Gender) : JobSeekerCommand<ProfileView>;

public sealed record UpdateLevel1Command(string FullName, string Email, string MobileNumber, string Gender, string? IfMatch) : JobSeekerCommand<Unit>;

public sealed record UpdateEducationCommand(IReadOnlyList<EducationInput> Entries, string? IfMatch) : JobSeekerCommand<Unit>;
public sealed record UpdateExperienceCommand(IReadOnlyList<ExperienceInput> Entries, decimal? YearsOfExperience, string? IfMatch) : JobSeekerCommand<Unit>;
public sealed record UpdateSkillsCommand(IReadOnlyList<SkillInput> Entries, string? IfMatch) : JobSeekerCommand<Unit>;
public sealed record UpdateTrainingCommand(IReadOnlyList<TrainingInput> Entries, string? IfMatch) : JobSeekerCommand<Unit>;
public sealed record UpdateCertificatesCommand(IReadOnlyList<CertificateInput> Entries, string? IfMatch) : JobSeekerCommand<Unit>;
public sealed record UpdateLevel3Command(IReadOnlyList<SocialLinkInput> SocialLinks, string? Statement, string? Bio, string? IfMatch) : JobSeekerCommand<Unit>;

public sealed record GetMyProfileQuery : JobSeekerQuery<ProfileView>;
public sealed record GetProfileCompletionRecommendationQuery : JobSeekerQuery<ProfileCompletionView>;

internal static class GenderValues
{
    public static readonly string[] Allowed = Enum.GetNames<Gender>();
}

public sealed class CreateProfileValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().Length(2, 100).WithErrorCode("VAL.FullName.Required");
        RuleFor(c => c.Email).NotEmpty().Must(e => Email.TryCreate(e, out _)).WithErrorCode("VAL.Email.Invalid");
        RuleFor(c => c.MobileNumber).NotEmpty().Must(m => MobileNumber.TryCreate(m, out _)).WithErrorCode("VAL.MobileNumber.Invalid");
        RuleFor(c => c.Gender).NotEmpty().Must(g => GenderValues.Allowed.Contains(g, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.Gender.Invalid");
    }
}

public sealed class UpdateLevel1Validator : AbstractValidator<UpdateLevel1Command>
{
    public UpdateLevel1Validator()
    {
        RuleFor(c => c.FullName).NotEmpty().Length(2, 100).WithErrorCode("VAL.FullName.Required");
        RuleFor(c => c.Email).NotEmpty().Must(e => Email.TryCreate(e, out _)).WithErrorCode("VAL.Email.Invalid");
        RuleFor(c => c.MobileNumber).NotEmpty().Must(m => MobileNumber.TryCreate(m, out _)).WithErrorCode("VAL.MobileNumber.Invalid");
        RuleFor(c => c.Gender).NotEmpty().Must(g => GenderValues.Allowed.Contains(g, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.Gender.Invalid");
    }
}

public sealed class UpdateEducationValidator : AbstractValidator<UpdateEducationCommand>
{
    public UpdateEducationValidator()
    {
        RuleForEach(c => c.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Degree).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Degree.Invalid");
            e.RuleFor(x => x.Institution).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Institution.Invalid");
            e.RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithErrorCode("VAL.DateRange.Invalid");
        });
    }
}

public sealed class UpdateExperienceValidator : AbstractValidator<UpdateExperienceCommand>
{
    public UpdateExperienceValidator()
    {
        RuleForEach(c => c.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Company).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Company.Invalid");
            e.RuleFor(x => x.Role).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Role.Invalid");
            e.RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithErrorCode("VAL.DateRange.Invalid");
        });
        RuleFor(c => c.YearsOfExperience).GreaterThanOrEqualTo(0).When(c => c.YearsOfExperience is not null).WithErrorCode("VAL.YearsOfExperience.Invalid");
    }
}

public sealed class UpdateSkillsValidator : AbstractValidator<UpdateSkillsCommand>
{
    public UpdateSkillsValidator()
    {
        RuleFor(c => c.Entries).Must(e => e.Count <= 100).WithErrorCode("VAL.Skills.TooMany");
        RuleForEach(c => c.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Skill.Invalid");
            e.RuleFor(x => x.Kind).Must(k => Enum.TryParse<SkillKind>(k, true, out _)).WithErrorCode("VAL.SkillKind.Invalid");
            e.RuleFor(x => x.Class).Must(k => Enum.TryParse<SkillClass>(k, true, out _)).WithErrorCode("VAL.SkillClass.Invalid");
        });
    }
}

public sealed class UpdateTrainingValidator : AbstractValidator<UpdateTrainingCommand>
{
    public UpdateTrainingValidator() =>
        RuleForEach(c => c.Entries).ChildRules(e => e.RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Training.Invalid"));
}

public sealed class UpdateCertificatesValidator : AbstractValidator<UpdateCertificatesCommand>
{
    public UpdateCertificatesValidator() =>
        RuleForEach(c => c.Entries).ChildRules(e => e.RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Certificate.Invalid"));
}

public sealed class UpdateLevel3Validator : AbstractValidator<UpdateLevel3Command>
{
    public UpdateLevel3Validator()
    {
        RuleFor(c => c.Statement).MaximumLength(1000).WithErrorCode("VAL.Statement.TooLong");
        RuleFor(c => c.Bio).MaximumLength(2000).WithErrorCode("VAL.Bio.TooLong");
        RuleForEach(c => c.SocialLinks).ChildRules(l =>
        {
            l.RuleFor(x => x.Network).NotEmpty().WithErrorCode("VAL.SocialLink.NetworkRequired");
            l.RuleFor(x => x.Url).NotEmpty().Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                .WithErrorCode("VAL.SocialLink.UrlInvalid");
        });
    }
}

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

internal sealed class GetMyProfileHandler(IProfileReadStore reads, ICurrentUser user) : IQueryHandler<GetMyProfileQuery, ProfileView>
{
    public async Task<Result<ProfileView>> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var view = await reads.GetByOwnerAsync(user.UserId!.Value, ct);
        return view is null ? Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.") : view;
    }
}

internal sealed class GetProfileCompletionRecommendationHandler(IProfileRepository profiles, ICurrentUser user)
    : IQueryHandler<GetProfileCompletionRecommendationQuery, ProfileCompletionView>
{
    public async Task<Result<ProfileCompletionView>> Handle(GetProfileCompletionRecommendationQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var (percent, missing) = profile.ComputeCompletionRecommendation();
        return new ProfileCompletionView(percent, missing);
    }
}

/// <summary>Loads the caller's profile and enforces the optional If-Match precondition as a 409 E-JSRPM-CONFLICT (Decision D-01: reject, not merge).</summary>
internal static class ProfileCommandSupport
{
    public static async Task<Result<ProfileAggregate>> LoadOwnedAsync(IProfileRepository profiles, ICurrentUser user, string? ifMatch, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        if (ifMatch is not null && !ETag.Matches(ifMatch, profile.RowVersion))
        {
            return Error.Conflict(ErrorCodes.Conflict, "The profile was modified since it was last read. Reload and retry.");
        }

        return profile;
    }
}

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

internal sealed class UpdateTrainingHandler(IProfileRepository profiles, ICurrentUser user, TimeProvider clock) : ICommandHandler<UpdateTrainingCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateTrainingCommand request, CancellationToken ct)
    {
        var loaded = await ProfileCommandSupport.LoadOwnedAsync(profiles, user, request.IfMatch, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.UpdateTraining(new Actor(user.UserId!.Value), request.Entries.Select(e => (e.Name, e.Provider, e.CompletedOn)).ToList(),
            user.UserId!.Value, clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

internal sealed class UpdateCertificatesHandler(IProfileRepository profiles, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<UpdateCertificatesCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateCertificatesCommand request, CancellationToken ct)
    {
        var loaded = await ProfileCommandSupport.LoadOwnedAsync(profiles, user, request.IfMatch, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.UpdateCertificates(new Actor(user.UserId!.Value), request.Entries.Select(e => (e.Name, e.Issuer, e.IssuedOn)).ToList(),
            user.UserId!.Value, clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

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
