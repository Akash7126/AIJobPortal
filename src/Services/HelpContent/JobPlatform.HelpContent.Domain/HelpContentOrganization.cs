using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

public enum HelpRole
{
    JobSeeker,
    Employer,
    ExternalJobSite,
    Guest,
    Administrator
}

/// <summary>AGG-32 catalogue entry: a help-navigation topic. Soft-removed so assigned articles never dangle (handover section 3.4:
/// a removed topic's articles fall back to "uncategorized").</summary>
public sealed class HelpTopic : AggregateRoot<Guid>
{
    private HelpTopic()
    {
    }

    public LocalizedText Name { get; private set; } = default!;

    public bool IsRemoved { get; private set; }

    public static HelpTopic Create(Guid id, LocalizedText name, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.OrganizationAdminOnly, ErrorCodes.HelpForbidden));
        Check(new BusinessRule(RuleCodes.HelpRequiredField, "The topic name is required.", name.IsEmpty, ErrorCodes.HelpRequiredField));
        return new HelpTopic { Id = id, Name = name };
    }

    public void Remove(Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.OrganizationAdminOnly, ErrorCodes.HelpForbidden));
        IsRemoved = true;
    }
}

/// <summary>
/// AGG-32: the topic and role structure a help article is filed under (1:1 per article, handover section 3.4). "Later save wins"
/// (story AC-03), same rationale as <see cref="ContentCategorization"/>: no RowVersion rejection.
/// </summary>
public sealed class HelpContentOrganization : AggregateRoot<Guid>
{
    private HelpContentOrganization()
    {
    }

    public Guid HelpContentId { get; private set; }

    public Guid? TopicId { get; private set; }

    public IReadOnlyList<HelpRole> Roles { get; private set; } = new List<HelpRole>();

    public static HelpContentOrganization CreateFor(Guid helpContentId) => new() { Id = helpContentId, HelpContentId = helpContentId };

    /// <summary>INV-11: a null or later-removed topic simply means "uncategorized" for readers - never rejected here.</summary>
    public void AssignTopicAndRoles(Guid? topicId, IReadOnlyList<HelpRole> roles, Actor actor)
    {
        Check(Rules.AdminOnly(actor, RuleCodes.OrganizationAdminOnly, ErrorCodes.HelpForbidden));
        TopicId = topicId;
        Roles = roles.Distinct().ToList();
    }
}
