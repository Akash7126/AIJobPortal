using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Ports;

namespace JobPlatform.AuditLogging.Application.Services.AuditLog;

/// <summary>Logic shared by the partner dashboard request handlers.</summary>
internal sealed class PartnerDashboardService
{
    private readonly ICurrentUser _user;

    public PartnerDashboardService(ICurrentUser user) => _user = user;

    public Guid Partner()
    {
        var viewer = new Viewer(_user.ActorType, _user.UserId);
        AccessScopePolicy.EnsureCanView(AuditCategory.ApiCall, viewer, OwnerScope.Of(OwnerType.Partner, viewer.Id ?? Guid.Empty));
        return viewer.Id!.Value;
    }
}
