using FluentValidation;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Application.Accounts;

// ---------------------------------------------------------------------- administrator actions (US-3.1.4-03)

public sealed record ApproveUserAccountCommand(Guid AccountId, string? IfMatch = null) : AdminAuthorized(Permissions.AccountsApprove, ErrorCodes.AdminForbidden), ICommand<Unit>;

public sealed record BanUserAccountCommand(Guid AccountId, string Reason, string? IfMatch = null) : AdminAuthorized(Permissions.AccountsBan, ErrorCodes.AdminForbidden), ICommand<Unit>;

public sealed record DeactivateUserAccountCommand(Guid AccountId, string Reason, string? IfMatch = null)
    : AdminAuthorized(Permissions.AccountsDeactivate, ErrorCodes.AdminForbidden), ICommand<Unit>;

public sealed record ResetCredentialsCommand(Guid AccountId, string? IfMatch = null)
    : AdminAuthorized(Permissions.AccountsResetCredentials, ErrorCodes.AdminForbidden), ICommand<Unit>;

public sealed record AssignRoleToAccountCommand(Guid AccountId, Guid RoleId, string? IfMatch = null)
    : AdminAuthorized(Permissions.AccountsAssignRoles, ErrorCodes.AdminForbidden), ICommand<Unit>;

public sealed record RemoveRoleFromAccountCommand(Guid AccountId, Guid RoleId, string? IfMatch = null)
    : AdminAuthorized(Permissions.AccountsAssignRoles, ErrorCodes.AdminForbidden), ICommand<Unit>;

public sealed class ApproveUserAccountValidator : AbstractValidator<ApproveUserAccountCommand>
{
    public ApproveUserAccountValidator() => RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}

public sealed class BanUserAccountValidator : AbstractValidator<BanUserAccountCommand>
{
    public BanUserAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("VAL.Reason.Required").MaximumLength(500).WithErrorCode("VAL.Reason.TooLong");
    }
}

public sealed class DeactivateUserAccountValidator : AbstractValidator<DeactivateUserAccountCommand>
{
    public DeactivateUserAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("VAL.Reason.Required").MaximumLength(500).WithErrorCode("VAL.Reason.TooLong");
    }
}

public sealed class ResetCredentialsValidator : AbstractValidator<ResetCredentialsCommand>
{
    public ResetCredentialsValidator() => RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}

public sealed class AssignRoleToAccountValidator : AbstractValidator<AssignRoleToAccountCommand>
{
    public AssignRoleToAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.RoleId).NotEmpty().WithErrorCode("VAL.RoleId.Required");
    }
}

public sealed class RemoveRoleFromAccountValidator : AbstractValidator<RemoveRoleFromAccountCommand>
{
    public RemoveRoleFromAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.RoleId).NotEmpty().WithErrorCode("VAL.RoleId.Required");
    }
}

internal sealed class AdminAccountHandlers :
    ICommandHandler<ApproveUserAccountCommand, Unit>,
    ICommandHandler<BanUserAccountCommand, Unit>,
    ICommandHandler<DeactivateUserAccountCommand, Unit>,
    ICommandHandler<ResetCredentialsCommand, Unit>,
    ICommandHandler<AssignRoleToAccountCommand, Unit>,
    ICommandHandler<RemoveRoleFromAccountCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly ISessionStore _sessions;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public AdminAccountHandlers(IAccountRepository accounts, ISessionStore sessions, ICurrentUser user, TimeProvider clock)
    {
        _accounts = accounts;
        _sessions = sessions;
        _user = user;
        _clock = clock;
    }

    private Actor Admin => Actor.Administrator(_user.UserId!.Value);

    public Task<Result<Unit>> Handle(ApproveUserAccountCommand request, CancellationToken ct) =>
        Run(request.AccountId, a => a.ApproveByAdministrator(Admin, _clock), invalidateSessions: false, request.IfMatch, ct);

    public Task<Result<Unit>> Handle(BanUserAccountCommand request, CancellationToken ct) =>
        Run(request.AccountId, a => a.Ban(Admin, request.Reason, _clock), invalidateSessions: true, request.IfMatch, ct);

    public Task<Result<Unit>> Handle(DeactivateUserAccountCommand request, CancellationToken ct) =>
        Run(request.AccountId, a => a.Deactivate(Admin, request.Reason, _clock), invalidateSessions: true, request.IfMatch, ct);

    public Task<Result<Unit>> Handle(ResetCredentialsCommand request, CancellationToken ct) =>
        Run(request.AccountId, a => a.ResetCredentials(Admin, _clock), invalidateSessions: true, request.IfMatch, ct);

    public Task<Result<Unit>> Handle(AssignRoleToAccountCommand request, CancellationToken ct) =>
        Run(request.AccountId, a => a.AssignRole(new RoleId(request.RoleId), _clock), invalidateSessions: false, request.IfMatch, ct);

    public Task<Result<Unit>> Handle(RemoveRoleFromAccountCommand request, CancellationToken ct) =>
        Run(request.AccountId, a => a.RemoveRole(new RoleId(request.RoleId), _clock), invalidateSessions: false, request.IfMatch, ct);

    private async Task<Result<Unit>> Run(Guid accountId, Action<Account> action, bool invalidateSessions, string? ifMatch, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(accountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        if (!ETag.Matches(ifMatch, account.RowVersion))
        {
            return ConcurrencyErrors.PreconditionFailed;
        }

        action(account);
        if (invalidateSessions)
        {
            // Ban, deactivation and credential reset end live sessions explicitly (Redis key session:{id} is evicted on ban).
            await _sessions.InvalidateAllForAccountAsync(account.Id.Value, null, ct);
        }

        return Result.Success();
    }
}

// ---------------------------------------------------------------------- deactivation requested by the owner via BC-04 (internal API)

/// <param name="Kind">"Deactivate" or "Delete".</param>
public sealed record RequestAccountDeactivationCommand(Guid AccountId, string Kind, string Reason)
    : ServiceAuthorized, ICommand<DeactivationRequestResultDto>;

public sealed class RequestAccountDeactivationValidator : AbstractValidator<RequestAccountDeactivationCommand>
{
    public RequestAccountDeactivationValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Kind).Must(k => k is "Deactivate" or "Delete").WithErrorCode("VAL.Kind.Invalid");
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("VAL.Reason.Required").MaximumLength(500).WithErrorCode("VAL.Reason.TooLong");
    }
}

internal sealed class RequestAccountDeactivationHandler : ICommandHandler<RequestAccountDeactivationCommand, DeactivationRequestResultDto>
{
    private readonly IAccountRepository _accounts;
    private readonly ISessionStore _sessions;
    private readonly TimeProvider _clock;

    public RequestAccountDeactivationHandler(IAccountRepository accounts, ISessionStore sessions, TimeProvider clock)
    {
        _accounts = accounts;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<Result<DeactivationRequestResultDto>> Handle(RequestAccountDeactivationCommand request, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(new AccountId(request.AccountId), ct);
        if (account is null)
        {
            return AccountErrors.NotFound;
        }

        var kind = request.Kind == "Delete" ? SuspensionKind.DeletionRequested : SuspensionKind.Deactivated;
        account.RequestDeactivation(kind, request.Reason, _clock);
        await _sessions.InvalidateAllForAccountAsync(account.Id.Value, null, ct);
        return new DeactivationRequestResultDto(account.Id.Value, kind.ToString(), _clock.GetUtcNow().UtcDateTime);
    }
}

// ---------------------------------------------------------------------- queries

public sealed record GetAccountStandingQuery(Guid AccountId) : AdminAuthorized(Permissions.AccountsRead, ErrorCodes.AdminForbidden), IQuery<AccountStandingView>;

public sealed record ListAccountsQuery(ActorType? ActorType, string? Standing, string? Search, int Page = 1, int PageSize = 20)
    : AdminAuthorized(Permissions.AccountsRead, ErrorCodes.AdminForbidden), IQuery<PagedResult<AccountListItemView>>;

public sealed record GetAccountSummaryQuery(Guid AccountId) : ServiceAuthorized, IQuery<AccountSummaryDto>;

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    private static readonly string[] Standings = { "Pending", "Active", "Banned", "Deactivated" };

    public ListAccountsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.Invalid");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.Invalid");
        When(x => !string.IsNullOrEmpty(x.Standing), () =>
            RuleFor(x => x.Standing!).Must(s => Standings.Contains(s)).WithErrorCode("VAL.Standing.Invalid"));
        When(x => !string.IsNullOrEmpty(x.Search), () =>
            RuleFor(x => x.Search!).MaximumLength(100).WithErrorCode("VAL.Search.TooLong"));
    }
}

internal sealed class AccountQueryHandlers :
    IQueryHandler<GetAccountStandingQuery, AccountStandingView>,
    IQueryHandler<ListAccountsQuery, PagedResult<AccountListItemView>>,
    IQueryHandler<GetAccountSummaryQuery, AccountSummaryDto>
{
    private readonly IIdentityReadStore _store;

    public AccountQueryHandlers(IIdentityReadStore store) => _store = store;

    public async Task<Result<AccountStandingView>> Handle(GetAccountStandingQuery request, CancellationToken ct)
    {
        var view = await _store.GetAccountStandingAsync(request.AccountId, ct);
        if (view is null)
        {
            return AccountErrors.NotFound;
        }

        return view with { Email = Masking.Email(view.Email), Mobile = Masking.Mobile(view.Mobile), AvailableActions = ActionsFor(view.Standing) };
    }

    public async Task<Result<PagedResult<AccountListItemView>>> Handle(ListAccountsQuery request, CancellationToken ct)
    {
        var page = await _store.ListAccountsAsync(new AccountListFilter(request.ActorType, request.Standing, request.Search),
            new PageRequest(request.Page, request.PageSize), ct);
        return page with { Items = page.Items.Select(i => i with { Email = Masking.Email(i.Email), Mobile = Masking.Mobile(i.Mobile) }).ToList() };
    }

    public async Task<Result<AccountSummaryDto>> Handle(GetAccountSummaryQuery request, CancellationToken ct)
    {
        var summary = await _store.GetAccountSummaryAsync(request.AccountId, ct);
        return summary is null ? AccountErrors.NotFound : summary;
    }

    /// <summary>Administrator actions per US-3.1.4-03 AC-02: approve, ban, deactivate, reset credentials, monitor status.</summary>
    internal static IReadOnlyList<string> ActionsFor(string standing) => standing switch
    {
        "Pending" => new[] { "approve", "ban", "reset-credentials", "monitor" },
        "Active" => new[] { "ban", "deactivate", "reset-credentials", "monitor" },
        "Deactivated" => new[] { "approve", "ban", "reset-credentials", "monitor" },
        _ => new[] { "reset-credentials", "monitor" }
    };
}
