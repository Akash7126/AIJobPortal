using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain;

/// <summary>AGG-19: a stored filter with an optional "notify me" subscription (US-3.2.2-04). No catalogued event on save (handover section 3.3).</summary>
public sealed class SavedSearch : AggregateRoot<Guid>
{
    private SavedSearch()
    {
        Criteria = new SearchCriteria(null, null, null, null, null, null, null, null, null);
        CriteriaHash = string.Empty;
    }

    public Guid OwnerAccountId { get; private set; }
    public SearchCriteria Criteria { get; private set; }
    public string CriteriaHash { get; private set; }
    public bool NotifyOnMatch { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastEvaluatedAtUtc { get; private set; }

    public static SavedSearch Save(Guid ownerAccountId, SearchCriteria criteria, bool notifyOnMatch, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        OwnerAccountId = ownerAccountId,
        Criteria = criteria,
        CriteriaHash = Hash(criteria),
        NotifyOnMatch = notifyOnMatch,
        CreatedAtUtc = nowUtc
    };

    /// <summary>US-3.2.2-04 AC-01: toggling whether a new match notifies the owner.</summary>
    public void SetNotify(bool notifyOnMatch, Actor actor)
    {
        Check(Rules.PostingOwnerOnly(actor, OwnerAccountId, ErrorCodes.FavoriteForbidden));
        NotifyOnMatch = notifyOnMatch;
    }

    /// <summary>US-3.2.2-04 AC-04: raises <c>SavedSearchMatched</c> when a new/published posting matches this opted-in search.</summary>
    public bool EvaluateMatch(JobPosting posting, DateTime nowUtc)
    {
        LastEvaluatedAtUtc = nowUtc;
        if (!NotifyOnMatch || !Criteria.Matches(posting))
        {
            return false;
        }

        Raise(new SavedSearchMatchedDomainEvent(nowUtc, Id, OwnerAccountId, posting.Id, posting.Title.En));
        return true;
    }

    /// <summary>Deterministic hash of the criteria (AC-02: an identical search reuses the existing one; foundation section 8 <c>CriteriaHash</c>).</summary>
    public static string Hash(SearchCriteria criteria)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            keyword = criteria.Keyword?.ToLowerInvariant(),
            governorate = criteria.Governorate?.ToLowerInvariant(),
            city = criteria.City?.ToLowerInvariant(),
            criteria.SalaryMin,
            criteria.SalaryMax,
            contractType = criteria.ContractType?.ToString(),
            criteria.PostedAfterUtc,
            criteria.DeadlineBeforeUtc,
            categoryCode = criteria.CategoryCode?.ToLowerInvariant()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
