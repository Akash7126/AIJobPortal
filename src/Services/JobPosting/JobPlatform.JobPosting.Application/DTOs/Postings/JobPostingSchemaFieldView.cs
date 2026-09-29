namespace JobPlatform.JobPosting.Application.DTOs.Postings;

public sealed record JobPostingSchemaFieldView(string Name, bool Required, string Type, IReadOnlyList<string>? AllowedValues);
