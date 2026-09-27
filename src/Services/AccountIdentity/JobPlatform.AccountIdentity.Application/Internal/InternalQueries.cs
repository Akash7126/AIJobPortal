using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Internal;

/// <summary>Access-log read for BC-07 (service token) or an administrator holding access-log.read (US-3.1.5-05).</summary>
public sealed record ListAccessLogQuery(DateTime? FromUtc, DateTime? ToUtc, Guid? AccountId, int Page = 1, int PageSize = 50)
    : IQuery<PagedResult<AccessLogEntryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.System, ActorType.Administrator };

    public string? RequiredPermission => Permissions.AccessLogRead;

    public bool RequireMfa => false;
}

/// <summary>Optional centralised permission check (default for other BCs is local JWT + role claim + cached map).</summary>
public sealed record CheckPermissionQuery(Guid AccountId, string Permission) : ServiceAuthorized, IQuery<PermissionCheckResultDto>;

public sealed class ListAccessLogValidator : AbstractValidator<ListAccessLogQuery>
{
    public ListAccessLogValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.Invalid");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.Invalid");
        RuleFor(x => x).Must(x => x.FromUtc is null || x.ToUtc is null || x.FromUtc <= x.ToUtc).OverridePropertyName("Range").WithErrorCode("VAL.Range.Invalid");
    }
}

public sealed class CheckPermissionValidator : AbstractValidator<CheckPermissionQuery>
{
    public CheckPermissionValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Permission).NotEmpty().WithErrorCode("VAL.Permission.Required").MaximumLength(100).WithErrorCode("VAL.Permission.TooLong");
    }
}

internal sealed class InternalQueryHandlers :
    IQueryHandler<ListAccessLogQuery, PagedResult<AccessLogEntryDto>>,
    IQueryHandler<CheckPermissionQuery, PermissionCheckResultDto>
{
    private readonly IIdentityReadStore _store;
    private readonly IRoleDirectory _roles;

    public InternalQueryHandlers(IIdentityReadStore store, IRoleDirectory roles)
    {
        _store = store;
        _roles = roles;
    }

    public async Task<Result<PagedResult<AccessLogEntryDto>>> Handle(ListAccessLogQuery request, CancellationToken ct) =>
        await _store.ListAccessLogAsync(request.FromUtc, request.ToUtc, request.AccountId, new PageRequest(request.Page, request.PageSize), ct);

    public async Task<Result<PermissionCheckResultDto>> Handle(CheckPermissionQuery request, CancellationToken ct)
    {
        var roles = await _roles.GetRolesForAccountAsync(request.AccountId, ct);
        var decision = AccessPolicy.Authorise(roles, request.Permission);
        return new PermissionCheckResultDto(request.AccountId, request.Permission, decision.Allowed);
    }
}
