namespace JobPlatform.AccountIdentity.Application.Interfaces;

public interface ICacheInvalidator
{
    Task InvalidateRoleMapAsync(CancellationToken ct = default);

    Task InvalidateAccountRolesAsync(Guid accountId, CancellationToken ct = default);

    Task InvalidatePasswordPolicyAsync(CancellationToken ct = default);
}
