using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

/// <summary>
/// Proposed aggregate + composed read model (handover section 3.6, story US-3.1.2-05): the page-background content this BC owns for an
/// employer's public company profile page. The full page view (company info/verification badge from BC-05, open postings from BC-09) is
/// composed at query time in the Application layer; this aggregate only holds what BC-06 itself is responsible for.
/// </summary>
public sealed class CompanyProfilePage : AggregateRoot<Guid>
{
    public const int MaxHighlights = 10;
    public const int MaxBackgroundLength = 5000;

    private CompanyProfilePage()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public LocalizedText Background { get; private set; } = new(null, null);

    public IReadOnlyList<string> Highlights { get; private set; } = new List<string>();

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static CompanyProfilePage OpenFor(Guid id, Guid employerAccountId, DateTime nowUtc) =>
        new() { Id = id, EmployerAccountId = employerAccountId, UpdatedAtUtc = nowUtc };

    /// <summary>Story AC-04: only the owning employer or an administrator may edit the page background.</summary>
    public void EditBackground(LocalizedText background, IReadOnlyList<string> highlights, Actor actor, DateTime nowUtc)
    {
        Check(Rules.OwnerOrAdminOnly(actor, EmployerAccountId, RuleCodes.CompanyPageOwnerOrAdminOnly, ErrorCodes.CompanyPageForbidden));
        Check(new BusinessRule("HC.CompanyPage.BACKGROUND_TOO_LONG", "The background must be 5000 characters or fewer.",
            (background.Ar?.Length ?? 0) > MaxBackgroundLength || (background.En?.Length ?? 0) > MaxBackgroundLength));
        Check(new BusinessRule("HC.CompanyPage.TOO_MANY_HIGHLIGHTS", "At most 10 highlights are allowed.", highlights.Count > MaxHighlights));

        Background = background;
        Highlights = highlights.ToList();
        PublishedAtUtc ??= nowUtc;
        UpdatedAtUtc = nowUtc;
        Raise(new CompanyPageChangedDomainEvent(EmployerAccountId, nowUtc));
    }
}
