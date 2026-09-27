using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Security;
using SecurityPermissions = JobPlatform.SharedKernel.Security.Permissions;

namespace JobPlatform.AccountIdentity.Domain.Rbac;

public static class RoleRuleCodes
{
    public const string UnknownPermission = "AI.Role.UNKNOWN_PERMISSION";
    public const string AdminOnly = "AI.Role.ADMIN_ONLY";
    public const string LastAdminPermission = "AI.Role.PROTECTED_PERMISSION";
}

public sealed class RolePermission
{
    private RolePermission()
    {
    }

    internal RolePermission(string permission) => Permission = permission;

    public string Permission { get; private set; } = string.Empty;
}

public sealed class Role : AggregateRoot<RoleId>
{
    private readonly List<RolePermission> _permissions = new();

    private Role()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public IReadOnlyCollection<RolePermission> Permissions => _permissions;

    public static Role Create(RoleId id, string name, bool isSystem, IEnumerable<string> permissions)
    {
        var role = new Role { Id = id, Name = name, IsSystem = isSystem };
        foreach (var permission in permissions)
        {
            role.AddPermission(permission);
        }

        return role;
    }

    public bool Has(string permission) => _permissions.Any(p => p.Permission == permission);

    /// <summary>Idempotent: granting an already-held permission is a no-op.</summary>
    public void GrantPermission(Actor admin, string permission, TimeProvider clock)
    {
        EnsureAdmin(admin);
        if (Has(permission))
        {
            EnsureKnown(permission);
            return;
        }

        AddPermission(permission);
        Raise(new RolePermissionsChangedDomainEvent(Id, clock.GetUtcNow().UtcDateTime));
    }

    public void RevokePermission(Actor admin, string permission, TimeProvider clock)
    {
        EnsureAdmin(admin);
        EnsureKnown(permission);
        var existing = _permissions.FirstOrDefault(p => p.Permission == permission);
        if (existing is null)
        {
            return;
        }

        // The administrator role must always be able to manage roles, or nobody could repair a mistake.
        Guard.Ensure(!(Id == WellKnownRoles.Administrator && permission == SecurityPermissions.RolesManage), RoleRuleCodes.LastAdminPermission,
            "The administrator role cannot lose the permission to manage roles.", ErrorCodes.AuthForbidden, BusinessRuleKind.BusinessRule);

        _permissions.Remove(existing);
        Raise(new RolePermissionsChangedDomainEvent(Id, clock.GetUtcNow().UtcDateTime));
    }

    public RolePermissions ToRolePermissions() => new(Id, Name, _permissions.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal));

    private void AddPermission(string permission)
    {
        EnsureKnown(permission);
        _permissions.Add(new RolePermission(permission));
    }

    private static void EnsureKnown(string permission) =>
        Guard.Ensure(SecurityPermissions.All.Contains(permission), RoleRuleCodes.UnknownPermission, $"Unknown permission '{permission}'.",
            ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);

    private static void EnsureAdmin(Actor admin) =>
        Guard.Ensure(admin.IsAdministrator, RoleRuleCodes.AdminOnly, "Administrator role required.", ErrorCodes.AuthForbidden,
            BusinessRuleKind.Forbidden);
}

/// <summary>Immutable role-to-permission snapshot; this is what the Redis-cached role map holds.</summary>
public sealed record RolePermissions(RoleId Id, string Name, IReadOnlySet<string> Permissions);

public sealed record AccessDecision(bool Allowed, string? Code, string? Reason)
{
    public static AccessDecision Allow() => new(true, null, null);

    public static AccessDecision Deny(string reason) => new(false, ErrorCodes.AuthForbidden, reason);
}

/// <summary>Domain service: RBAC evaluation (US-3.1.5-03). A user may act when any of their roles holds the permission.</summary>
public static class AccessPolicy
{
    public static AccessDecision Authorise(IEnumerable<RolePermissions> userRoles, string permission)
    {
        foreach (var role in userRoles)
        {
            if (role.Permissions.Contains(permission))
            {
                return AccessDecision.Allow();
            }
        }

        return AccessDecision.Deny($"No role grants '{permission}'.");
    }
}

public sealed record RolePermissionsChangedDomainEvent(RoleId RoleId, DateTime At) : DomainEvent(At);

/// <summary>Deterministic ids of the built-in roles so tokens, seed data and tests agree without a lookup.</summary>
public static class WellKnownRoles
{
    public static readonly RoleId JobSeeker = new(new Guid("00000000-0000-4000-8000-000000000101"));
    public static readonly RoleId Employer = new(new Guid("00000000-0000-4000-8000-000000000102"));
    public static readonly RoleId Administrator = new(new Guid("00000000-0000-4000-8000-000000000103"));
    public static readonly RoleId ExternalJobSite = new(new Guid("00000000-0000-4000-8000-000000000104"));
    public static readonly RoleId Guest = new(new Guid("00000000-0000-4000-8000-000000000105"));

    public static RoleId ForActor(ActorType actorType) => actorType switch
    {
        ActorType.JobSeeker => JobSeeker,
        ActorType.Employer => Employer,
        ActorType.Administrator => Administrator,
        ActorType.ExternalJobSite => ExternalJobSite,
        _ => Guest
    };

    public static IReadOnlyList<(RoleId Id, string Name, string[] Permissions)> Definitions { get; } = new (RoleId, string, string[])[]
    {
        (JobSeeker, Roles.JobSeeker, new[] { Permissions.ProfileManage, Permissions.JobsBrowse }),
        (Employer, Roles.Employer, new[] { Permissions.EmployerManage, Permissions.JobsBrowse }),
        (ExternalJobSite, Roles.ExternalJobSite, new[] { Permissions.ApiCredentialsManage, Permissions.JobsImport }),
        (Guest, Roles.Guest, new[] { Permissions.JobsBrowse }),
        (Administrator, Roles.Administrator, Permissions.All.ToArray())
    };
}
