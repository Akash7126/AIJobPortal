namespace JobPlatform.BuildingBlocks.Api.Interfaces.Security;

public interface IRemotePermissionChecker
{
    Task<bool> IsAllowedAsync(Guid accountId, string permission, CancellationToken ct);
}
