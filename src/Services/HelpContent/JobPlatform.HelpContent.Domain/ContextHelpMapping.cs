using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

/// <summary>
/// Proposed aggregate (handover section 3.8, story US-3.7.2-04): PageKey -&gt; HelpContentId, admin-edited. An unmapped page key falls back
/// to the general help center (AC-02) - resolved by the query returning null, never an error.
/// </summary>
public sealed class ContextHelpMapping : AggregateRoot<Guid>
{
    private ContextHelpMapping()
    {
    }

    public string PageKey { get; private set; } = string.Empty;

    public Guid HelpContentId { get; private set; }

    public static ContextHelpMapping Create(Guid id, string pageKey, Guid helpContentId, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.ContextHelpMappingAdminOnly, ErrorCodes.HelpForbidden));
        return new ContextHelpMapping { Id = id, PageKey = pageKey, HelpContentId = helpContentId };
    }

    public void Repoint(Guid helpContentId, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.ContextHelpMappingAdminOnly, ErrorCodes.HelpForbidden));
        HelpContentId = helpContentId;
    }
}
