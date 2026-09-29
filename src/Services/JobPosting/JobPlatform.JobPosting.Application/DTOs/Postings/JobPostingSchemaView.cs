namespace JobPlatform.JobPosting.Application.DTOs.Postings;

public sealed record JobPostingSchemaView(int TaxonomyVersion, IReadOnlyList<JobPostingSchemaFieldView> Fields, IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<string> SkillCodes);
