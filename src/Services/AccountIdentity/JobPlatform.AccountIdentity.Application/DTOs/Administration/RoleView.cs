namespace JobPlatform.AccountIdentity.Application.DTOs.Administration;

/// <param name="ETag">Strong tag of the role (RowVersion): send it as If-Match when changing the role. Lists have no single ETag header, so it travels in the body.</param>
public sealed record RoleView(Guid RoleId, string Name, bool IsSystem, IReadOnlyList<string> Permissions, string ETag = "");
