using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Queries.Internal;

/// <summary>Access-log read for BC-07 (service token) or an administrator holding access-log.read (US-3.1.5-05).</summary>
public sealed record ListAccessLogQuery(DateTime? FromUtc, DateTime? ToUtc, Guid? AccountId, int Page = 1, int PageSize = 50)
    : IQuery<PagedResult<AccessLogEntryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System, ActorType.Administrator };

    public string? RequiredPermission => Permissions.AccessLogRead;

    public bool RequireMfa => false;
}
