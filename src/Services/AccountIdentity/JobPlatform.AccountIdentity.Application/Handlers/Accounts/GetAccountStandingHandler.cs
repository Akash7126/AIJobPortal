using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Application.Queries.Accounts;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AccountIdentity.Application.Handlers.Accounts;

internal sealed class GetAccountStandingHandler : IQueryHandler<GetAccountStandingQuery, AccountStandingView>
{
    private readonly IIdentityReadStore _store;

    public GetAccountStandingHandler(IIdentityReadStore store) => _store = store;

    public async Task<Result<AccountStandingView>> Handle(GetAccountStandingQuery request, CancellationToken ct)
    {
        var view = await _store.GetAccountStandingAsync(request.AccountId, ct);
        if (view is null)
        {
            return AccountErrors.NotFound;
        }

        return view with { Email = Masking.Email(view.Email), Mobile = Masking.Mobile(view.Mobile), AvailableActions = ActionsFor(view.Standing) };
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
