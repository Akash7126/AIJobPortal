using JobPlatform.CandidateSourcing.Application.DTOs.TalentPool;

namespace JobPlatform.CandidateSourcing.Application.Queries.TalentPool;

public sealed record ListTalentPoolQuery : EmployerQuery<IReadOnlyList<TalentPoolEntryView>>;
