using FluentValidation;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Privacy;

public sealed record SetProfileVisibilityCommand(bool Public, bool PublicSharingActive) : JobSeekerCommand<Unit>;
public sealed record RequestAccountDeactivationCommand(string? Reason) : JobSeekerCommand<Unit>;
public sealed record RequestAccountDeletionCommand(bool Confirm) : JobSeekerCommand<Unit>;
public sealed record GetPrivacySettingQuery : JobSeekerQuery<PrivacySettingView>;

public sealed class RequestAccountDeletionValidator : AbstractValidator<RequestAccountDeletionCommand>
{
    public RequestAccountDeletionValidator() => RuleFor(c => c.Confirm).Equal(true).WithErrorCode("VAL.Confirm.Required");
}

internal static class PrivacySupport
{
    public static async Task<Result<(Domain.Profile Profile, PrivacySetting Setting, bool IsNew)>> LoadAsync(IProfileRepository profiles,
        IPrivacySettingRepository settings, ICurrentUser user, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var setting = await settings.GetByProfileAsync(profile.Id, ct);
        var isNew = setting is null;
        setting ??= PrivacySetting.CreateDefault(profile.Id);
        setting.EnsureOwnedBy(new Actor(user.UserId!.Value), profile.OwnerAccountId);
        return (profile, setting, isNew);
    }
}

internal sealed class SetProfileVisibilityHandler(IProfileRepository profiles, IPrivacySettingRepository settings, ICurrentUser user)
    : ICommandHandler<SetProfileVisibilityCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetProfileVisibilityCommand request, CancellationToken ct)
    {
        var loaded = await PrivacySupport.LoadAsync(profiles, settings, user, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.Setting.SetVisibility(request.Public, request.PublicSharingActive);
        if (loaded.Value.IsNew)
        {
            settings.Add(loaded.Value.Setting);
        }

        return Result.Success();
    }
}

internal sealed class RequestAccountDeactivationHandler(IProfileRepository profiles, IPrivacySettingRepository settings, IAccountIdentityClient identity,
    ICurrentUser user, TimeProvider clock) : ICommandHandler<RequestAccountDeactivationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RequestAccountDeactivationCommand request, CancellationToken ct)
    {
        var loaded = await PrivacySupport.LoadAsync(profiles, settings, user, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.Setting.RequestDeactivation(clock.GetUtcNow().UtcDateTime);
        if (loaded.Value.IsNew)
        {
            settings.Add(loaded.Value.Setting);
        }

        await identity.RequestDeactivationAsync(loaded.Value.Profile.OwnerAccountId, request.Reason ?? "JobSeekerRequested", "Deactivated", ct);
        return Result.Success();
    }
}

internal sealed class RequestAccountDeletionHandler(IProfileRepository profiles, IPrivacySettingRepository settings, IAccountIdentityClient identity,
    ICurrentUser user, TimeProvider clock) : ICommandHandler<RequestAccountDeletionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RequestAccountDeletionCommand request, CancellationToken ct)
    {
        var loaded = await PrivacySupport.LoadAsync(profiles, settings, user, ct);
        if (loaded.IsFailure)
        {
            return Result.Failure<Unit>(loaded.Error!);
        }

        loaded.Value.Setting.RequestDeletion(clock.GetUtcNow().UtcDateTime);
        if (loaded.Value.IsNew)
        {
            settings.Add(loaded.Value.Setting);
        }

        await identity.RequestDeactivationAsync(loaded.Value.Profile.OwnerAccountId, "DeletionRequested", "DeletionRequested", ct);
        return Result.Success();
    }
}

internal sealed class GetPrivacySettingHandler(IProfileRepository profiles, IPrivacySettingRepository settings, ICurrentUser user)
    : IQueryHandler<GetPrivacySettingQuery, PrivacySettingView>
{
    public async Task<Result<PrivacySettingView>> Handle(GetPrivacySettingQuery request, CancellationToken ct)
    {
        var profile = await profiles.GetByOwnerAsync(user.UserId!.Value, ct);
        if (profile is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "No profile exists for this account.");
        }

        var setting = await settings.GetByProfileAsync(profile.Id, ct) ?? PrivacySetting.CreateDefault(profile.Id);
        return new PrivacySettingView(setting.ProfileId, setting.Visibility.ToString(), setting.PublicSharingActive, setting.DeletionState.ToString(),
            setting.DeactivationRequestedAtUtc);
    }
}
