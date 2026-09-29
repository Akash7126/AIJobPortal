namespace JobPlatform.JobPosting.Application.Queries.InternalApi;

public sealed record CheckReferenceUsageQuery(string Type, IReadOnlyList<string> Codes) : ServiceQuery<IReadOnlyList<string>>;
