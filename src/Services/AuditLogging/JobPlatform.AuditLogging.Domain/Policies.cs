using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain;

/// <summary>The caller as the visibility policy needs to know it.</summary>
public sealed record Viewer(ActorType? ActorType, Guid? Id)
{
    public bool IsAdministrator => ActorType == SharedKernel.Common.Enums.ActorType.Administrator;
}

/// <summary>
/// Decides visibility per category (handover 3.6): partners see their own logs, employers their own postings' history, users their own
/// notification history; access, admin, government, e-mail, SMS and job-audit logs are administrator-only. Each refusal carries the story's error code.
/// </summary>
public static class AccessScopePolicy
{
    public static string ForbiddenCode(AuditCategory category) => category switch
    {
        AuditCategory.ApiCall or AuditCategory.Submission or AuditCategory.SyncError => AuditErrorCodes.PartnerForbidden,
        AuditCategory.JobStatus => AuditErrorCodes.JobStatusForbidden,
        AuditCategory.Notification => AuditErrorCodes.NotificationForbidden,
        AuditCategory.Access => AuditErrorCodes.AccessForbidden,
        AuditCategory.GovernmentExchange => AuditErrorCodes.GovernmentForbidden,
        AuditCategory.Email => AuditErrorCodes.EmailForbidden,
        AuditCategory.Sms => AuditErrorCodes.SmsForbidden,
        AuditCategory.Insight => AuditErrorCodes.InsightForbidden,
        _ => AuditErrorCodes.AdminForbidden
    };

    public static bool AdministratorOnly(AuditCategory category) => category is AuditCategory.Access or AuditCategory.AdminAction
        or AuditCategory.JobAudit or AuditCategory.GovernmentExchange or AuditCategory.Email or AuditCategory.Sms or AuditCategory.Profile;

    /// <summary>The owner type whose members may see the category (null = administrators only).</summary>
    public static OwnerType? OwnerTypeFor(AuditCategory category) => category switch
    {
        AuditCategory.ApiCall or AuditCategory.Submission or AuditCategory.SyncError => OwnerType.Partner,
        AuditCategory.JobStatus or AuditCategory.Insight => OwnerType.Employer,
        AuditCategory.Notification => OwnerType.User,
        _ => null
    };

    public static bool CanView(AuditCategory category, Viewer viewer, OwnerScope owner)
    {
        if (viewer.ActorType is null || viewer.Id is null)
        {
            return false;
        }

        if (viewer.IsAdministrator)
        {
            // Administrators see everything except the owners' private views of their own dashboards.
            return true;
        }

        if (AdministratorOnly(category))
        {
            return false;
        }

        var expectedOwner = OwnerTypeFor(category);
        var expectedActor = expectedOwner switch
        {
            OwnerType.Partner => SharedKernel.Common.Enums.ActorType.ExternalJobSite,
            OwnerType.Employer => SharedKernel.Common.Enums.ActorType.Employer,
            _ => (ActorType?)null
        };
        if (expectedOwner is null || owner.Type != expectedOwner || owner.OwnerId != viewer.Id)
        {
            return false;
        }

        return expectedActor is null || viewer.ActorType == expectedActor;
    }

    /// <summary>Throws the category's forbidden error unless the viewer may see entries with this owner scope.</summary>
    public static void EnsureCanView(AuditCategory category, Viewer viewer, OwnerScope owner)
    {
        if (CanView(category, viewer, owner))
        {
            return;
        }

        var adminOnly = AdministratorOnly(category);
        throw new BusinessRuleViolationException(adminOnly ? AuditRuleCodes.AdminOnly : AuditRuleCodes.NotOwner,
            adminOnly ? "Only administrators may view this log." : "You may only view your own entries.",
            ForbiddenCode(category), BusinessRuleKind.Forbidden);
    }
}

/// <summary>12 months live by default (A-02-012, THR-048/082), then archived - never purged.</summary>
public sealed class RetentionPolicy
{
    public const int DefaultMonths = 12;

    public RetentionPolicy(int months = DefaultMonths)
    {
        Guard.Ensure(months is >= 1 and <= 120, AuditRuleCodes.RetentionInvalid, "Retention must be between 1 and 120 months.");
        Months = months;
    }

    public int Months { get; }

    public DateTime RetainUntil(DateTime occurredAtUtc) => occurredAtUtc.AddMonths(Months);

    /// <summary>An entry is expired once its retain-until instant has passed (exactly at the boundary it is still live).</summary>
    public bool IsExpired(DateTime retainUntilUtc, DateTime nowUtc) => nowUtc > retainUntilUtc;
}

/// <summary>Candidate privacy: a withheld field is reported as unavailable, never as a default value (3.3.3-06 AC-02/03).</summary>
public static class CandidateInsightPolicy
{
    public const string Unavailable = "unavailable";

    public static string Availability(string? value, IReadOnlyCollection<string> withheld) =>
        IsWithheld("availability", withheld) || string.IsNullOrWhiteSpace(value) ? Unavailable : value;

    public static string ExpectedSalary(decimal? value, IReadOnlyCollection<string> withheld) =>
        IsWithheld("expectedSalary", withheld) || value is null ? Unavailable : value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public static string Fit(decimal? value, IReadOnlyCollection<string> withheld) =>
        IsWithheld("fitScore", withheld) || value is null ? Unavailable : value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsWithheld(string field, IReadOnlyCollection<string> withheld) =>
        withheld.Any(w => string.Equals(w, field, StringComparison.OrdinalIgnoreCase));
}
