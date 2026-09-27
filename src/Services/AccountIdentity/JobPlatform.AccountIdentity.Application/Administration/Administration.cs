using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Administration;

// ---------------------------------------------------------------------- password policy (US-3.1.5-02)

public sealed record ConfigurePasswordPolicyCommand(int MinLength, bool RequireUpper, bool RequireLower, bool RequireDigit, string? IfMatch = null)
    : AdminAuthorized(Permissions.PasswordPolicyManage), ICommand<Unit>;

public sealed record GetPasswordPolicyQuery : AdminAuthorized, IQuery<PasswordPolicyView>
{
    public GetPasswordPolicyQuery() : base(Permissions.PasswordPolicyManage)
    {
    }
}

public sealed class ConfigurePasswordPolicyValidator : AbstractValidator<ConfigurePasswordPolicyCommand>
{
    public ConfigurePasswordPolicyValidator()
    {
        RuleFor(x => x.MinLength).InclusiveBetween(8, 128).WithErrorCode("VAL.MinLength.Range");
        RuleFor(x => x).Must(x => x.RequireUpper || x.RequireLower || x.RequireDigit).OverridePropertyName("CharacterClasses")
            .WithErrorCode("VAL.CharacterClasses.Required");
    }
}

internal sealed class PasswordPolicyHandlers :
    ICommandHandler<ConfigurePasswordPolicyCommand, Unit>,
    IQueryHandler<GetPasswordPolicyQuery, PasswordPolicyView>
{
    private readonly IPasswordPolicyRepository _policies;
    private readonly IIdentityReadStore _store;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PasswordPolicyHandlers(IPasswordPolicyRepository policies, IIdentityReadStore store, ICurrentUser user, TimeProvider clock)
    {
        _policies = policies;
        _store = store;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ConfigurePasswordPolicyCommand request, CancellationToken ct)
    {
        var policy = await _policies.GetAsync(ct);
        if (!ETag.Matches(request.IfMatch, policy.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        policy.Configure(Actor.Administrator(_user.UserId!.Value), request.MinLength, request.RequireUpper, request.RequireLower, request.RequireDigit, _clock);
        return Result.Success();
    }

    public async Task<Result<PasswordPolicyView>> Handle(GetPasswordPolicyQuery request, CancellationToken ct) =>
        await _store.GetPasswordPolicyAsync(ct);
}

// ---------------------------------------------------------------------- session timeout (US-3.1.5-04)

public sealed record ConfigureSessionTimeoutCommand(int IdleTimeoutMinutes, string? IfMatch = null) : AdminAuthorized(Permissions.SessionTimeoutManage), ICommand<Unit>;

public sealed record GetSessionTimeoutQuery : AdminAuthorized, IQuery<SessionTimeoutView>
{
    public GetSessionTimeoutQuery() : base(Permissions.SessionTimeoutManage)
    {
    }
}

public sealed class ConfigureSessionTimeoutValidator : AbstractValidator<ConfigureSessionTimeoutCommand>
{
    public ConfigureSessionTimeoutValidator() =>
        RuleFor(x => x.IdleTimeoutMinutes).InclusiveBetween(AccountDefaults.MinIdleTimeoutMinutes, AccountDefaults.MaxIdleTimeoutMinutes)
            .WithErrorCode("VAL.IdleTimeoutMinutes.Range");
}

internal sealed class SessionTimeoutHandlers :
    ICommandHandler<ConfigureSessionTimeoutCommand, Unit>,
    IQueryHandler<GetSessionTimeoutQuery, SessionTimeoutView>
{
    private readonly ISessionTimeoutSettingRepository _settings;
    private readonly IIdentityReadStore _store;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public SessionTimeoutHandlers(ISessionTimeoutSettingRepository settings, IIdentityReadStore store, ICurrentUser user, TimeProvider clock)
    {
        _settings = settings;
        _store = store;
        _user = user;
        _clock = clock;
    }

    /// <summary>Only sessions created afterwards use the new value: existing sessions captured theirs at creation (AC-03).</summary>
    public async Task<Result<Unit>> Handle(ConfigureSessionTimeoutCommand request, CancellationToken ct)
    {
        var setting = await _settings.GetAsync(ct);
        if (!ETag.Matches(request.IfMatch, setting.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        setting.Configure(Actor.Administrator(_user.UserId!.Value), request.IdleTimeoutMinutes, _clock);
        return Result.Success();
    }

    public async Task<Result<SessionTimeoutView>> Handle(GetSessionTimeoutQuery request, CancellationToken ct) =>
        await _store.GetSessionTimeoutAsync(ct);
}

// ---------------------------------------------------------------------- roles & permissions (US-3.1.5-03)

public sealed record GrantPermissionCommand(Guid RoleId, string Permission, string? IfMatch = null) : AdminAuthorized(Permissions.RolesManage), ICommand<Unit>;

public sealed record RevokePermissionCommand(Guid RoleId, string Permission, string? IfMatch = null) : AdminAuthorized(Permissions.RolesManage), ICommand<Unit>;

public sealed record ListRolesQuery : AdminAuthorized, IQuery<IReadOnlyList<RoleView>>
{
    public ListRolesQuery() : base(Permissions.RolesRead)
    {
    }
}

public sealed class GrantPermissionValidator : AbstractValidator<GrantPermissionCommand>
{
    public GrantPermissionValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithErrorCode("VAL.RoleId.Required");
        RuleFor(x => x.Permission).NotEmpty().WithErrorCode("VAL.Permission.Required").MaximumLength(100).WithErrorCode("VAL.Permission.TooLong");
    }
}

public sealed class RevokePermissionValidator : AbstractValidator<RevokePermissionCommand>
{
    public RevokePermissionValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithErrorCode("VAL.RoleId.Required");
        RuleFor(x => x.Permission).NotEmpty().WithErrorCode("VAL.Permission.Required").MaximumLength(100).WithErrorCode("VAL.Permission.TooLong");
    }
}

internal sealed class RoleHandlers :
    ICommandHandler<GrantPermissionCommand, Unit>,
    ICommandHandler<RevokePermissionCommand, Unit>,
    IQueryHandler<ListRolesQuery, IReadOnlyList<RoleView>>
{
    private static readonly Error RoleNotFound = Error.NotFound("E-ROLE-NOT-FOUND", "The role was not found.");

    private readonly IRoleRepository _roles;
    private readonly IIdentityReadStore _store;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RoleHandlers(IRoleRepository roles, IIdentityReadStore store, ICurrentUser user, TimeProvider clock)
    {
        _roles = roles;
        _store = store;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(GrantPermissionCommand request, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(new RoleId(request.RoleId), ct);
        if (role is null)
        {
            return RoleNotFound;
        }

        if (!ETag.Matches(request.IfMatch, role.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        role.GrantPermission(Actor.Administrator(_user.UserId!.Value), request.Permission, _clock);
        return Result.Success();
    }

    public async Task<Result<Unit>> Handle(RevokePermissionCommand request, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(new RoleId(request.RoleId), ct);
        if (role is null)
        {
            return RoleNotFound;
        }

        if (!ETag.Matches(request.IfMatch, role.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        role.RevokePermission(Actor.Administrator(_user.UserId!.Value), request.Permission, _clock);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<RoleView>>> Handle(ListRolesQuery request, CancellationToken ct) =>
        Result.Success(await _store.ListRolesAsync(ct));
}
