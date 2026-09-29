using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Services.AuditLog;

/// <summary>Logic shared by the audit log query request handlers.</summary>
internal sealed class AuditLogQueryService
{
    private readonly IAuditReadStore _store;
    private readonly ICurrentUser _user;

    public AuditLogQueryService(IAuditReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    /// <summary>Applies the scope policy for the category, then reads only what the caller may see (the owner's own rows for owned categories).</summary>
    public async Task<Result<PagedResult<AuditEntryDto>>> Owned(AuditCategory category, IFilteredListQuery q, bool includeArchived, string? subjectId, CancellationToken ct)
    {
        var viewer = new Viewer(_user.ActorType, _user.UserId);
        var ownerType = AccessScopePolicy.OwnerTypeFor(category);
        var scope = ownerType is not null && viewer.Id is { } id ? OwnerScope.Of(ownerType.Value, id) : OwnerScope.AdminOnly;
        AccessScopePolicy.EnsureCanView(category, viewer, scope);

        var outcome = string.IsNullOrEmpty(q.Outcome) ? (AuditOutcome?)null : Enum.Parse<AuditOutcome>(q.Outcome, true);
        var filter = new EntryFilter(new[] { category }, ownerType is null ? null : viewer.Id, subjectId, q.From, q.To, outcome, includeArchived);
        return await _store.ListEntriesAsync(filter, new PageRequest(q.Page, q.PageSize), ct);
    }
}
