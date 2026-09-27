namespace JobPlatform.CandidateSourcing.Domain.Privacy;

public enum CandidateVisibility
{
    Public,
    Private
}

/// <summary>
/// Domain-local restatement of a candidate's privacy state (translated by the application from BC-04's published DTOs, so the domain never
/// depends on another BC's contract shape). Field names in <see cref="DisclosedFields"/> follow BC-04's vocabulary (skills, education,
/// experience, location, salary, availability, languages, ...).
/// </summary>
public sealed record CandidateSnapshot(
    Guid ProfileId, CandidateVisibility Visibility, bool EmployerVisibilityOptIn, bool Deactivated, IReadOnlyCollection<string> DisclosedFields);

/// <summary>
/// CS.Privacy.* (US-3.3.3-05, US-3.3.3-07 AC-04): a private candidate is excluded from every employer-facing path unless they separately opted
/// in to employer visibility (BC-11 Q-07); a deactivated candidate is always excluded; only disclosed fields are ever shown to an employer.
/// </summary>
public static class CandidatePrivacyPolicy
{
    /// <summary>True when the candidate may appear in recommendations, ranking, search and talent-pool views.</summary>
    public static bool IsVisibleToEmployers(CandidateSnapshot candidate) =>
        !candidate.Deactivated && (candidate.Visibility == CandidateVisibility.Public || candidate.EmployerVisibilityOptIn);

    public static bool IsFieldDisclosed(CandidateSnapshot candidate, string field) =>
        candidate.DisclosedFields.Contains(field, StringComparer.OrdinalIgnoreCase);
}
