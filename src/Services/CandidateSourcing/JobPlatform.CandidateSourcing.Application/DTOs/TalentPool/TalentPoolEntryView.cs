namespace JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;

public sealed record TalentPoolEntryView(Guid TalentPoolEntryId, Guid CandidateProfileId, Guid JobPostingId, string? Note, DateTime AddedAtUtc);
