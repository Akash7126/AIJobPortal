using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;

namespace JobPlatform.CandidateSourcing.Application.Commands.TalentPool;

/// <summary>US-3.3.3-07: save a candidate to the employer's talent pool. Idempotent per (employer, candidate, posting) - AC-02.</summary>
public sealed record AddToTalentPoolCommand(Guid CandidateProfileId, Guid JobPostingId, string? Note) : EmployerCommand<TalentPoolEntryView>;
