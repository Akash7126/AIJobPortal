namespace JobPlatform.AuditLogging.Application.Interfaces;

/// <summary>Common filter of the log queries: optional time window, outcome class (success | failure | duplicate) and paging.</summary>
public interface IFilteredListQuery
{
    DateTime? From { get; }
    DateTime? To { get; }
    string? Outcome { get; }
    int Page { get; }
    int PageSize { get; }
}
