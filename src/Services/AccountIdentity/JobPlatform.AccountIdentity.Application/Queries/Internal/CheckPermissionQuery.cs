using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AccountIdentity.Application.Queries.Internal;

/// <summary>Optional centralised permission check (default for other BCs is local JWT + role claim + cached map).</summary>
public sealed record CheckPermissionQuery(Guid AccountId, string Permission) : ServiceAuthorized, IQuery<PermissionCheckResultDto>;
